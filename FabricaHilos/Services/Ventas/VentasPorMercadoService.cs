using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class VentasPorMercadoService : IVentasPorMercadoService
    {
        private readonly string _baseConnectionString;
        private readonly ILogger<VentasPorMercadoService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public VentasPorMercadoService(
            IConfiguration configuration,
            ILogger<VentasPorMercadoService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _baseConnectionString = configuration.GetConnectionString("OracleConnection")
                ?? throw new InvalidOperationException("Oracle connection string not found.");
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetOracleConnectionString()
        {
            var oraUser = _httpContextAccessor.HttpContext?.Session.GetString("OracleUser");
            var oraPass = _httpContextAccessor.HttpContext?.Session.GetString("OraclePass");

            if (!string.IsNullOrEmpty(oraUser) && !string.IsNullOrEmpty(oraPass))
            {
                var csb = new OracleConnectionStringBuilder(_baseConnectionString)
                {
                    UserID = oraUser,
                    Password = oraPass
                };
                return csb.ToString();
            }

            return _baseConnectionString;
        }

        private static string? GetStr(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : r[col]?.ToString();

        private static decimal GetDec(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

        // ─────────────────────────────────────────────────────────
        // Ventas agrupadas por Mercado: Perú / Latam / Global
        // ─────────────────────────────────────────────────────────
        // Lógica de clasificación:
        //   - Si C.PAIS = 'PE' → Perú
        //   - Si TABLAS_AUXILIARES(tipo=25, codigo=C.PAIS).INDICADOR1 = 'L' → Latam
        //   - Todo lo demás (E=Europa, O=Oceanía, A=Asia, otros) → Global
        // ─────────────────────────────────────────────────────────
        public async Task<List<VentaMercadoDto>> ObtenerVentasPorMercadoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<VentaMercadoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT MERCADO, SUM(IMPORTE) IMPORTE
  FROM (
    SELECT CASE
             WHEN C.PAIS = 'PE' THEN 'Perú'
             WHEN NVL(TA.INDICADOR1, 'X') = 'L' THEN 'LATAM'
             ELSE 'Global'
           END MERCADO,
           DECODE(:P_MON,
                  'S', DECODE(D.MONEDA,
                              'S', D.IMP_NETO,
                              ROUND(D.IMP_NETO * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D', D.IMP_NETO,
                         ROUND(D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0), 2))) IMPORTE
      FROM DOCUVENT D
      JOIN CLIENTES C   ON C.COD_CLIENTE = D.COD_CLIENTE
      LEFT JOIN TABLAS_AUXILIARES TA ON TA.TIPO = 25 AND TA.CODIGO = C.PAIS
     WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
       AND D.ESTADO <> '9'
  )
 GROUP BY MERCADO
 ORDER BY DECODE(MERCADO, 'Perú', 1, 'LATAM', 2, 3)";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new VentaMercadoDto
                    {
                        Mercado = GetStr(reader, "MERCADO"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Ventas por Mercado");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle por País (drill-down desde el donut)
        // ─────────────────────────────────────────────────────────
        public async Task<List<VentaMercadoPaisDto>> ObtenerDetallePorPaisAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<VentaMercadoPaisDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT MERCADO, CODIGO_PAIS, PAIS_NOMBRE,
       SUM(IMPORTE) IMPORTE
  FROM (
    SELECT CASE
             WHEN C.PAIS = 'PE' THEN 'Perú'
             WHEN NVL(TA.INDICADOR1, 'X') = 'L' THEN 'LATAM'
             ELSE 'Global'
           END MERCADO,
           C.PAIS CODIGO_PAIS,
           NVL(TA.DESCRIPCION, C.PAIS) PAIS_NOMBRE,
           DECODE(:P_MON,
                  'S', DECODE(D.MONEDA,
                              'S', D.IMP_NETO,
                              ROUND(D.IMP_NETO * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D', D.IMP_NETO,
                         ROUND(D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0), 2))) IMPORTE
      FROM DOCUVENT D
      JOIN CLIENTES C   ON C.COD_CLIENTE = D.COD_CLIENTE
      LEFT JOIN TABLAS_AUXILIARES TA ON TA.TIPO = 25 AND TA.CODIGO = C.PAIS
     WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
       AND D.ESTADO <> '9'
  )
 WHERE (:P_MERCADO IS NULL OR MERCADO = :P_MERCADO)
 GROUP BY MERCADO, CODIGO_PAIS, PAIS_NOMBRE
 ORDER BY IMPORTE DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",     OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1",  OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2",  OracleDbType.Date).Value     = fechaFin.Date;
                cmd.Parameters.Add("P_MERCADO", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(mercado) ? (object)DBNull.Value : mercado;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new VentaMercadoPaisDto
                    {
                        Mercado    = GetStr(reader, "MERCADO"),
                        CodigoPais = GetStr(reader, "CODIGO_PAIS"),
                        Pais       = GetStr(reader, "PAIS_NOMBRE"),
                        Importe    = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle por País");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle por Departamento (solo Perú)
        // ─────────────────────────────────────────────────────────
        public async Task<List<VentaMercadoDepartamentoDto>> ObtenerDetallePorDepartamentoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<VentaMercadoDepartamentoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT NVL(U.NOM_DPT, 'Sin Departamento') DEPARTAMENTO,
       SUM(DECODE(:P_MON,
                  'S', DECODE(D.MONEDA,
                              'S', D.IMP_NETO,
                              ROUND(D.IMP_NETO * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D', D.IMP_NETO,
                         ROUND(D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0), 2)))) IMPORTE
  FROM DOCUVENT D
  JOIN CLIENTES C ON C.COD_CLIENTE = D.COD_CLIENTE
  JOIN UBIGEO U   ON U.COD_UBC = C.COD_UBC
 WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND D.ESTADO <> '9'
   AND C.PAIS = 'PE'
 GROUP BY NVL(U.NOM_DPT, 'Sin Departamento')
 ORDER BY IMPORTE DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new VentaMercadoDepartamentoDto
                    {
                        Departamento = GetStr(reader, "DEPARTAMENTO"),
                        Importe      = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle por Departamento");
            }

            return result;
        }
    }
}
