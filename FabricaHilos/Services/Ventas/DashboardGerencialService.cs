using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class DashboardGerencialService : IDashboardGerencialService
    {
        private readonly string _baseConnectionString;
        private readonly ILogger<DashboardGerencialService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DashboardGerencialService(
            IConfiguration configuration,
            ILogger<DashboardGerencialService> logger,
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
        public async Task<List<DgVentaMercadoDto>> ObtenerVentasPorMercadoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT MERCADO, SUM(IMPORTE) IMPORTE
  FROM (
    SELECT CASE
             WHEN C.PAIS = '01' THEN 'Perú'
             WHEN NVL(TA.INDICADOR1, 'X') = 'L' THEN 'LATAM'
             WHEN NVL(TA.INDICADOR1, 'X') = 'E' THEN 'Europa'
             WHEN NVL(TA.INDICADOR1, 'X') = 'A' THEN 'Asia'
             WHEN NVL(TA.INDICADOR1, 'X') = 'O' THEN 'Oceanía'
             ELSE 'Otros'
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
 ORDER BY DECODE(MERCADO, 'Perú', 1, 'LATAM', 2, 'Europa', 3, 'Asia', 4, 'Oceanía', 5, 6)";

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
                    result.Add(new DgVentaMercadoDto
                    {
                        Mercado = GetStr(reader, "MERCADO"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Ventas por Mercado (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle por País (drill-down desde el donut)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoPaisDto>> ObtenerDetallePorPaisAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoPaisDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT MERCADO, CODIGO_PAIS, PAIS_NOMBRE,
       SUM(IMPORTE) IMPORTE
  FROM (
    SELECT CASE
             WHEN C.PAIS = '01' THEN 'Perú'
             WHEN NVL(TA.INDICADOR1, 'X') = 'L' THEN 'LATAM'
             WHEN NVL(TA.INDICADOR1, 'X') = 'E' THEN 'Europa'
             WHEN NVL(TA.INDICADOR1, 'X') = 'A' THEN 'Asia'
             WHEN NVL(TA.INDICADOR1, 'X') = 'O' THEN 'Oceanía'
             ELSE 'Otros'
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
                    result.Add(new DgVentaMercadoPaisDto
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
                _logger.LogError(ex, "Error al obtener detalle por País (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle por Departamento (solo Perú, UBIGEO.PAIS='01')
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoDepartamentoDto>> ObtenerDetallePorDepartamentoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoDepartamentoDto>();
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
   AND U.PAIS = '01'
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
                    result.Add(new DgVentaMercadoDepartamentoDto
                    {
                        Departamento = GetStr(reader, "DEPARTAMENTO"),
                        Importe      = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle por Departamento (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle por Distrito dentro de un Departamento (Perú)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoDistritoDto>> ObtenerDetallePorDistritoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string departamento)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoDistritoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT NVL(U.NOM_DPT, 'Sin Departamento') DEPARTAMENTO,
       NVL(U.NOM_DTT, 'Sin Distrito') DISTRITO,
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
   AND U.PAIS = '01'
   AND UPPER(NVL(U.NOM_DPT, 'Sin Departamento')) = UPPER(:P_DPTO)
 GROUP BY NVL(U.NOM_DPT, 'Sin Departamento'), NVL(U.NOM_DTT, 'Sin Distrito')
 ORDER BY IMPORTE DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;
                cmd.Parameters.Add("P_DPTO",   OracleDbType.Varchar2).Value = departamento ?? "";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoDistritoDto
                    {
                        Departamento = GetStr(reader, "DEPARTAMENTO"),
                        Distrito     = GetStr(reader, "DISTRITO"),
                        Importe      = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle por Distrito del departamento {Departamento} (Dashboard Gerencial)", departamento);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle de Ciudades por País Extranjero
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoCiudadPaisDto>> ObtenerCiudadesPorPaisAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string codigoPais)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoCiudadPaisDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT NVL(U.NOM_DPT, C.PAIS) PAIS_NOMBRE,
       NVL(U.NOM_DTT, 'Sin Ciudad') CIUDAD,
       SUM(DECODE(:P_MON,
                  'S', DECODE(D.MONEDA,
                              'S', D.IMP_NETO,
                              ROUND(D.IMP_NETO * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D', D.IMP_NETO,
                         ROUND(D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0), 2)))) IMPORTE
  FROM DOCUVENT D
  JOIN CLIENTES C   ON C.COD_CLIENTE = D.COD_CLIENTE
  LEFT JOIN UBIGEO U ON U.COD_UBC = C.COD_UBC
 WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND D.ESTADO <> '9'
   AND C.PAIS = :P_PAIS
 GROUP BY NVL(U.NOM_DPT, C.PAIS), NVL(U.NOM_DTT, 'Sin Ciudad')
 ORDER BY IMPORTE DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;
                cmd.Parameters.Add("P_PAIS",   OracleDbType.Varchar2).Value = codigoPais ?? "";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoCiudadPaisDto
                    {
                        Pais   = GetStr(reader, "PAIS_NOMBRE"),
                        Ciudad = GetStr(reader, "CIUDAD"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ciudades del país {Pais} (Dashboard Gerencial)", codigoPais);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Evolución mensual por mercado
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoEvolucionDto>> ObtenerEvolucionMensualAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoEvolucionDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT PERIODO, MERCADO, SUM(IMPORTE) IMPORTE
  FROM (
    SELECT TO_CHAR(D.FECHA, 'YYYY-MM') PERIODO,
           CASE
             WHEN C.PAIS = '01' THEN 'Perú'
             WHEN NVL(TA.INDICADOR1, 'X') = 'L' THEN 'LATAM'
             WHEN NVL(TA.INDICADOR1, 'X') = 'E' THEN 'Europa'
             WHEN NVL(TA.INDICADOR1, 'X') = 'A' THEN 'Asia'
             WHEN NVL(TA.INDICADOR1, 'X') = 'O' THEN 'Oceanía'
             ELSE 'Otros'
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
 GROUP BY PERIODO, MERCADO
 ORDER BY PERIODO, DECODE(MERCADO, 'Perú', 1, 'LATAM', 2, 'Europa', 3, 'Asia', 4, 'Oceanía', 5, 6)";

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
                    result.Add(new DgVentaMercadoEvolucionDto
                    {
                        Periodo = GetStr(reader, "PERIODO"),
                        Mercado = GetStr(reader, "MERCADO"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener evolución mensual por Mercado (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Top Clientes por Mercado
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoTopClienteDto>> ObtenerTopClientesAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoTopClienteDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            string sql = $@"
SELECT * FROM (
  SELECT COD_CLIENTE, NOM_CLIENTE, PAIS, MERCADO,
         SUM(IMPORTE) IMPORTE, COUNT(*) CANT_DOCS
    FROM (
      SELECT D.COD_CLIENTE,
             NVL(C.NOMBRE, D.COD_CLIENTE) NOM_CLIENTE,
              C.PAIS,
              CASE
                WHEN C.PAIS = '01' THEN 'Perú'
                WHEN NVL(TA.INDICADOR1, 'X') = 'L' THEN 'LATAM'
                WHEN NVL(TA.INDICADOR1, 'X') = 'E' THEN 'Europa'
                WHEN NVL(TA.INDICADOR1, 'X') = 'A' THEN 'Asia'
                WHEN NVL(TA.INDICADOR1, 'X') = 'O' THEN 'Oceanía'
                ELSE 'Otros'
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
   WHERE (:P_MERCADO IS NULL OR MERCADO = :P_MERCADO)
   GROUP BY COD_CLIENTE, NOM_CLIENTE, PAIS, MERCADO
   ORDER BY IMPORTE DESC
) WHERE ROWNUM <= :P_TOP";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",     OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1",  OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2",  OracleDbType.Date).Value     = fechaFin.Date;
                cmd.Parameters.Add("P_MERCADO", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(mercado) ? (object)DBNull.Value : mercado;
                cmd.Parameters.Add("P_TOP",     OracleDbType.Int32).Value    = top > 0 ? top : 15;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoTopClienteDto
                    {
                        CodCliente     = GetStr(reader, "COD_CLIENTE"),
                        NomCliente     = GetStr(reader, "NOM_CLIENTE"),
                        Pais           = GetStr(reader, "PAIS"),
                        Mercado        = GetStr(reader, "MERCADO"),
                        Importe        = GetDec(reader, "IMPORTE"),
                        CantDocumentos = Convert.ToInt32(reader["CANT_DOCS"])
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener top clientes por Mercado (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle completo de Documentos (nivel transaccional)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoDocumentoDto>> ObtenerDetalleDocumentosAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoDocumentoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT D.TIPODOC, D.SERIE, D.NUMERO, D.FECHA, D.MONEDA,
       D.COD_CLIENTE, NVL(C.NOMBRE, D.COD_CLIENTE) NOM_CLIENTE,
       C.PAIS, NVL(D.EXPORTACION, 'N') EXPORTACION,
       CASE
         WHEN C.PAIS = '01' THEN 'Perú'
         WHEN NVL(TA.INDICADOR1, 'X') = 'L' THEN 'LATAM'
         WHEN NVL(TA.INDICADOR1, 'X') = 'E' THEN 'Europa'
         WHEN NVL(TA.INDICADOR1, 'X') = 'A' THEN 'Asia'
         WHEN NVL(TA.INDICADOR1, 'X') = 'O' THEN 'Oceanía'
         ELSE 'Otros'
       END MERCADO,
       D.IMPORT_CAM, D.VAL_VENTA, D.IMP_DESCTO, D.IMP_ANTICIPO,
       D.IMP_INTERES, D.IMP_NETO, D.IMP_IGV, D.PRECIO_VTA,
       NVL(U.NOM_DPT, '') DEPARTAMENTO,
       NVL(U.NOM_DTT, '') DISTRITO,
       NVL(U.PAIS, C.PAIS) UBIGEO_PAIS
  FROM DOCUVENT D
  JOIN CLIENTES C   ON C.COD_CLIENTE = D.COD_CLIENTE
  LEFT JOIN UBIGEO U ON U.COD_UBC = C.COD_UBC
  LEFT JOIN TABLAS_AUXILIARES TA ON TA.TIPO = 25 AND TA.CODIGO = C.PAIS
 WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND D.ESTADO <> '9'
   AND (:P_MERCADO IS NULL OR
        CASE
          WHEN C.PAIS = '01' THEN 'Perú'
          WHEN NVL(TA.INDICADOR1, 'X') = 'L' THEN 'LATAM'
          WHEN NVL(TA.INDICADOR1, 'X') = 'E' THEN 'Europa'
          WHEN NVL(TA.INDICADOR1, 'X') = 'A' THEN 'Asia'
          WHEN NVL(TA.INDICADOR1, 'X') = 'O' THEN 'Oceanía'
          ELSE 'Otros'
        END = :P_MERCADO)
 ORDER BY D.FECHA DESC, D.TIPODOC, D.SERIE, D.NUMERO";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_FECHA1",  OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2",  OracleDbType.Date).Value     = fechaFin.Date;
                cmd.Parameters.Add("P_MERCADO", OracleDbType.Varchar2).Value = string.IsNullOrEmpty(mercado) ? (object)DBNull.Value : mercado;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoDocumentoDto
                    {
                        TipoDoc      = GetStr(reader, "TIPODOC"),
                        Serie        = GetStr(reader, "SERIE"),
                        Numero       = GetStr(reader, "NUMERO"),
                        Fecha        = reader["FECHA"] == DBNull.Value ? null : Convert.ToDateTime(reader["FECHA"]),
                        Moneda       = GetStr(reader, "MONEDA"),
                        CodCliente   = GetStr(reader, "COD_CLIENTE"),
                        NomCliente   = GetStr(reader, "NOM_CLIENTE"),
                        Pais         = GetStr(reader, "PAIS"),
                        Exportacion  = GetStr(reader, "EXPORTACION"),
                        Mercado      = GetStr(reader, "MERCADO"),
                        ImportCam    = GetDec(reader, "IMPORT_CAM"),
                        ValVenta     = GetDec(reader, "VAL_VENTA"),
                        ImpDescto    = GetDec(reader, "IMP_DESCTO"),
                        ImpAnticipo  = GetDec(reader, "IMP_ANTICIPO"),
                        ImpInteres   = GetDec(reader, "IMP_INTERES"),
                        ImpNeto      = GetDec(reader, "IMP_NETO"),
                        ImpIgv       = GetDec(reader, "IMP_IGV"),
                        PrecioVta    = GetDec(reader, "PRECIO_VTA"),
                        Departamento = GetStr(reader, "DEPARTAMENTO"),
                        Distrito     = GetStr(reader, "DISTRITO"),
                        UbigeoPais   = GetStr(reader, "UBIGEO_PAIS")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de documentos (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Mapeo de países BD → ISO (TABLAS_AUXILIARES TIPO=25)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgPaisIsoDto>> ObtenerPaisesIsoAsync()
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgPaisIsoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT CODIGO, INDICADOR2, DESCRIPCION
  FROM TABLAS_AUXILIARES
 WHERE TIPO = 25
 ORDER BY CODIGO";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd    = new OracleCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgPaisIsoDto
                    {
                        CodigoBD    = GetStr(reader, "CODIGO"),
                        CodigoISO   = GetStr(reader, "INDICADOR2"),
                        Descripcion = GetStr(reader, "DESCRIPCION")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener mapeo de países ISO (Dashboard Gerencial)");
            }

            return result;
        }
    }
}
