using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class DashboardGerencialService : OracleServiceBase, IDashboardGerencialService
    {
        private readonly ILogger<DashboardGerencialService> _logger;

        public DashboardGerencialService(
            IConfiguration configuration,
            ILogger<DashboardGerencialService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        private static string? GetStr(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : r[col]?.ToString();

        private static decimal GetDec(OracleDataReader r, string col)
        {
            var ordinal = r.GetOrdinal(col);
            if (r.IsDBNull(ordinal)) return 0m;
            var oraVal = r.GetOracleDecimal(ordinal);
            // Truncar a la precisión de .NET decimal (28 dígitos) para evitar OverflowException
            oraVal = OracleDecimal.SetPrecision(oraVal, 28);
            return oraVal.Value;
        }

        // ─────────────────────────────────────────────────────────
        // Ventas agrupadas por Mercado: Perú / Latam / Global
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoDto>> ObtenerVentasPorMercadoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            var sql = $@"
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
                               D.IMP_NETO * D.IMPORT_CAM),
                   DECODE(D.MONEDA,
                          'D', D.IMP_NETO,
                          D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0))) IMPORTE
      FROM {S}DOCUVENT D
      JOIN {S}CLIENTES C   ON C.COD_CLIENTE = D.COD_CLIENTE
      LEFT JOIN (SELECT CODIGO, MAX(INDICADOR1) INDICADOR1
                   FROM {S}TABLAS_AUXILIARES WHERE TIPO = 25
                  GROUP BY CODIGO) TA ON TA.CODIGO = C.PAIS
     WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
       AND NVL(D.ESTADO, '0') <> '9'
        AND NVL(D.ORIGEN, '0') <> 'A'
        AND EXISTS (SELECT 1 FROM {S}ITEMDOCU I2
                     JOIN {S}ARTICUL M2 ON M2.COD_ART = I2.COD_ART
                    WHERE I2.TIPODOC = D.TIPODOC AND I2.SERIE = D.SERIE AND I2.NUMERO = D.NUMERO
                      AND M2.TP_ART IN ('T', 'S'))
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

            var sql = $@"
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
                               D.IMP_NETO * D.IMPORT_CAM),
                   DECODE(D.MONEDA,
                          'D', D.IMP_NETO,
                          D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0))) IMPORTE
      FROM {S}DOCUVENT D
      JOIN {S}CLIENTES C   ON C.COD_CLIENTE = D.COD_CLIENTE
      LEFT JOIN (SELECT CODIGO, MAX(INDICADOR1) INDICADOR1,
                        MAX(DESCRIPCION) DESCRIPCION
                   FROM {S}TABLAS_AUXILIARES WHERE TIPO = 25
                  GROUP BY CODIGO) TA ON TA.CODIGO = C.PAIS
     WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
       AND NVL(D.ESTADO, '0') <> '9'
        AND NVL(D.ORIGEN, '0') <> 'A'
        AND EXISTS (SELECT 1 FROM {S}ITEMDOCU I2
                     JOIN {S}ARTICUL M2 ON M2.COD_ART = I2.COD_ART
                    WHERE I2.TIPODOC = D.TIPODOC AND I2.SERIE = D.SERIE AND I2.NUMERO = D.NUMERO
                      AND M2.TP_ART IN ('T', 'S'))
   )
  WHERE (:P_MERCADO IS NULL OR MERCADO = :P_MERCADO
        OR (:P_MERCADO = 'Global' AND MERCADO IN ('Europa','Asia','Oceanía','Otros')))
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

            var sql = $@"
SELECT NVL(U.NOM_DPT, 'Sin Departamento') DEPARTAMENTO,
       SUM(DECODE(:P_MON,
                  'S', DECODE(D.MONEDA,
                              'S', D.IMP_NETO,
                              ROUND(D.IMP_NETO * D.IMPORT_CAM, 2)),
                  DECODE(D.MONEDA,
                         'D', D.IMP_NETO,
                         ROUND(D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0), 2)))) IMPORTE
  FROM {S}DOCUVENT D
  JOIN {S}CLIENTES C    ON C.COD_CLIENTE = D.COD_CLIENTE
  LEFT JOIN {S}UBIGEO U ON U.COD_UBC = C.COD_UBC
 WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND NVL(D.ESTADO, '0') <> '9'
    AND NVL(D.ORIGEN, '0') <> 'A'
    AND (U.PAIS = '01' OR U.COD_UBC IS NULL)
    AND EXISTS (SELECT 1 FROM {S}ITEMDOCU I2
                  JOIN {S}ARTICUL M2 ON M2.COD_ART = I2.COD_ART
                 WHERE I2.TIPODOC = D.TIPODOC AND I2.SERIE = D.SERIE AND I2.NUMERO = D.NUMERO
                   AND M2.TP_ART IN ('T', 'S'))
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

            var sql = $@"
SELECT NVL(U.NOM_DPT, 'Sin Departamento') DEPARTAMENTO,
       NVL(U.NOM_DTT, 'Sin Distrito') DISTRITO,
       SUM(DECODE(:P_MON,
                   'S', DECODE(D.MONEDA,
                               'S', D.IMP_NETO,
                               D.IMP_NETO * D.IMPORT_CAM),
                   DECODE(D.MONEDA,
                          'D', D.IMP_NETO,
                          D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0)))) IMPORTE
   FROM {S}DOCUVENT D
   JOIN {S}CLIENTES C    ON C.COD_CLIENTE = D.COD_CLIENTE
   LEFT JOIN {S}UBIGEO U ON U.COD_UBC = C.COD_UBC
  WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
    AND NVL(D.ESTADO, '0') <> '9'
     AND NVL(D.ORIGEN, '0') <> 'A'
     AND (U.PAIS = '01' OR U.COD_UBC IS NULL)
     AND UPPER(NVL(U.NOM_DPT, 'Sin Departamento')) = UPPER(:P_DPTO)
   AND EXISTS (SELECT 1 FROM {S}ITEMDOCU I2
                 JOIN {S}ARTICUL M2 ON M2.COD_ART = I2.COD_ART
                WHERE I2.TIPODOC = D.TIPODOC AND I2.SERIE = D.SERIE AND I2.NUMERO = D.NUMERO
                  AND M2.TP_ART IN ('T', 'S'))
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

            var sql = $@"
SELECT NVL(U.NOM_DPT, C.PAIS) PAIS_NOMBRE,
       NVL(U.NOM_DTT, 'Sin Ciudad') CIUDAD,
       SUM(DECODE(:P_MON,
                   'S', DECODE(D.MONEDA,
                               'S', D.IMP_NETO,
                               D.IMP_NETO * D.IMPORT_CAM),
                   DECODE(D.MONEDA,
                          'D', D.IMP_NETO,
                          D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0)))) IMPORTE
   FROM {S}DOCUVENT D
   JOIN {S}CLIENTES C   ON C.COD_CLIENTE = D.COD_CLIENTE
   LEFT JOIN {S}UBIGEO U ON U.COD_UBC = C.COD_UBC
 WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND NVL(D.ESTADO, '0') <> '9'
    AND NVL(D.ORIGEN, '0') <> 'A'
    AND C.PAIS = :P_PAIS
   AND EXISTS (SELECT 1 FROM {S}ITEMDOCU I2
                 JOIN {S}ARTICUL M2 ON M2.COD_ART = I2.COD_ART
                WHERE I2.TIPODOC = D.TIPODOC AND I2.SERIE = D.SERIE AND I2.NUMERO = D.NUMERO
                  AND M2.TP_ART IN ('T', 'S'))
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

            var sql = $@"
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
                              D.IMP_NETO * D.IMPORT_CAM),
                  DECODE(D.MONEDA,
                         'D', D.IMP_NETO,
                         D.IMP_NETO / NULLIF(D.IMPORT_CAM, 0))) IMPORTE
      FROM {S}DOCUVENT D
      JOIN {S}CLIENTES C   ON C.COD_CLIENTE = D.COD_CLIENTE
      LEFT JOIN (SELECT CODIGO, MAX(INDICADOR1) INDICADOR1
                   FROM {S}TABLAS_AUXILIARES WHERE TIPO = 25
                  GROUP BY CODIGO) TA ON TA.CODIGO = C.PAIS
     WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
       AND NVL(D.ESTADO, '0') <> '9'
        AND NVL(D.ORIGEN, '0') <> 'A'
        AND EXISTS (SELECT 1 FROM {S}ITEMDOCU I2
                     JOIN {S}ARTICUL M2 ON M2.COD_ART = I2.COD_ART
                    WHERE I2.TIPODOC = D.TIPODOC AND I2.SERIE = D.SERIE AND I2.NUMERO = D.NUMERO
                      AND M2.TP_ART IN ('T', 'S'))
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
        // Mapeo de países BD → ISO (TABLAS_AUXILIARES TIPO=25)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgPaisIsoDto>> ObtenerPaisesIsoAsync()
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgPaisIsoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            var sql = $@"
SELECT CODIGO, INDICADOR2, DESCRIPCION
  FROM {S}TABLAS_AUXILIARES
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

        // ─────────────────────────────────────────────────────────
        // Cantidad KG mensual (sin filtro de asesor)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgKgMensualDto>> ObtenerKgMensualAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgKgMensualDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            var sql = $@"
SELECT TO_CHAR(A.FECHA, 'YYYY-MM') PERIODO,
       SUM(I.CANTIDAD * E.FACTOR)  CANTIDAD_KG
  FROM {S}DOCUVENT A
  LEFT JOIN {S}ITEMDOCU I     ON I.TIPODOC = A.TIPODOC AND I.SERIE = A.SERIE AND I.NUMERO = A.NUMERO
  LEFT JOIN {S}EQUIVALENCIA E ON E.COD_ART = I.COD_ART AND E.UNIDAD = 'KG'
  LEFT JOIN {S}ARTICUL M      ON M.COD_ART = I.COD_ART
 WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND NVL(A.ESTADO, '0') <> '9'
    AND NVL(A.ORIGEN, '0') <> 'A'
    AND M.TP_ART IN ('T', 'S')
 GROUP BY TO_CHAR(A.FECHA, 'YYYY-MM')
 ORDER BY 1";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgKgMensualDto
                    {
                        Periodo    = GetStr(reader, "PERIODO"),
                        CantidadKg = GetDec(reader, "CANTIDAD_KG")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener KG mensual (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Top Hilados por Importe (agrupado por familia TFAMLIN)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgTopHiladoImporteDto>> ObtenerTopHiladosImporteAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgTopHiladoImporteDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            var sql = $@"
SELECT FAMILIA, IMPORTE FROM (
  SELECT NVL(F.DESCRIPCION, 'SIN FAMILIA') FAMILIA,
         DECODE(:P_MON,
           'S',
             SUM(DECODE(A.MONEDA,
                   'S', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
                        ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * A.IMPORT_CAM)))
           + SUM(DECODE(A.MONEDA,
                   'S', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV,
                        ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV) * A.IMPORT_CAM)),
             SUM(DECODE(A.MONEDA,
                   'D', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
                        ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) / NULLIF(A.IMPORT_CAM, 0))))
           + SUM(DECODE(A.MONEDA,
                   'D', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV,
                        ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV) / NULLIF(A.IMPORT_CAM, 0)))
         ) IMPORTE
    FROM {S}DOCUVENT A
    LEFT JOIN {S}ITEMDOCU I     ON I.TIPODOC = A.TIPODOC AND I.SERIE = A.SERIE AND I.NUMERO = A.NUMERO
    LEFT JOIN {S}ARTICUL M      ON M.COD_ART = I.COD_ART
    LEFT JOIN {S}TFAMLIN F      ON F.COD_FAM = M.COD_FAM AND F.COD_LIN = M.COD_LIN
   WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
     AND NVL(A.ESTADO, '0') <> '9'
      AND NVL(A.ORIGEN, '0') <> 'A'
      AND M.TP_ART IN ('T', 'S')
   GROUP BY NVL(F.DESCRIPCION, 'SIN FAMILIA')
   ORDER BY IMPORTE DESC
) WHERE ROWNUM <= :P_TOP";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;
                cmd.Parameters.Add("P_TOP",    OracleDbType.Int32).Value    = top;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgTopHiladoImporteDto
                    {
                        Familia = GetStr(reader, "FAMILIA"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Top Hilados por Importe (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Ventas por Giro de Cliente
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaPorGiroDto>> ObtenerVentasPorGiroAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaPorGiroDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            var sql = $@"
SELECT CODIGO_GIRO,
       NVL((SELECT MAX(T2.ABREVIADA)
              FROM {S}TABLAS_AUXILIARES T2
             WHERE T2.TIPO = 27 AND T2.CODIGO = CODIGO_GIRO), 'SIN GIRO') DESC_GIRO,
       SUM(IMPORTE_CLI) IMPORTE
  FROM (
    SELECT C.GIRO CODIGO_GIRO,
           DECODE(:P_MON,
             'S',
               SUM(DECODE(A.MONEDA,
                     'S', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
                          ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * A.IMPORT_CAM)))
             + SUM(DECODE(A.MONEDA,
                     'S', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV,
                          ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV) * A.IMPORT_CAM)),
               SUM(DECODE(A.MONEDA,
                     'D', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
                          ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) / NULLIF(A.IMPORT_CAM, 0))))
             + SUM(DECODE(A.MONEDA,
                     'D', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV,
                          ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV) / NULLIF(A.IMPORT_CAM, 0)))
           ) IMPORTE_CLI
      FROM {S}DOCUVENT A
      LEFT JOIN {S}ITEMDOCU I ON I.TIPODOC = A.TIPODOC AND I.SERIE = A.SERIE AND I.NUMERO = A.NUMERO
      LEFT JOIN {S}ARTICUL M  ON M.COD_ART = I.COD_ART
      LEFT JOIN {S}CLIENTES C ON C.COD_CLIENTE = A.COD_CLIENTE
     WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
       AND NVL(A.ESTADO, '0') <> '9'
       AND NVL(A.ORIGEN, '0') <> 'A'
       AND M.TP_ART IN ('T', 'S')
       AND UPPER(NVL((SELECT MAX(T3.DESCRIPCION)
                  FROM {S}TABLAS_AUXILIARES T3
                 WHERE T3.TIPO = 29 AND T3.CODIGO = C.VENDEDOR), '')) <> 'OFICINA'
     GROUP BY A.COD_CLIENTE, C.GIRO
    HAVING DECODE(:P_MON,
             'S',
               SUM(DECODE(A.MONEDA,
                     'S', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
                          ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * A.IMPORT_CAM)))
             + SUM(DECODE(A.MONEDA,
                     'S', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV,
                          ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV) * A.IMPORT_CAM)),
               SUM(DECODE(A.MONEDA,
                     'D', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
                          ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) / NULLIF(A.IMPORT_CAM, 0))))
             + SUM(DECODE(A.MONEDA,
                     'D', (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV,
                          ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV) / NULLIF(A.IMPORT_CAM, 0)))
           ) > 0
  )
 GROUP BY CODIGO_GIRO
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
                    result.Add(new DgVentaPorGiroDto
                    {
                        CodigoGiro = GetStr(reader, "CODIGO_GIRO"),
                        DescGiro   = GetStr(reader, "DESC_GIRO"),
                        Importe    = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Ventas por Giro (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Top Hilados por KG (agrupado por descripción artículo)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgTopHiladoKgDto>> ObtenerTopHiladosKgAsync(
            DateTime fechaInicio, DateTime fechaFin, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgTopHiladoKgDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            var sql = $@"
SELECT FAMILIA, KILOS FROM (
  SELECT NVL(F.DESCRIPCION, 'SIN FAMILIA') FAMILIA,
         SUM(I.CANTIDAD * E.FACTOR) KILOS
    FROM {S}DOCUVENT A
    LEFT JOIN {S}ITEMDOCU I     ON I.TIPODOC = A.TIPODOC AND I.SERIE = A.SERIE AND I.NUMERO = A.NUMERO
    LEFT JOIN {S}EQUIVALENCIA E ON E.COD_ART = I.COD_ART AND E.UNIDAD = 'KG'
    LEFT JOIN {S}ARTICUL M      ON M.COD_ART = I.COD_ART
    LEFT JOIN {S}TFAMLIN F      ON F.COD_FAM = M.COD_FAM AND F.COD_LIN = M.COD_LIN
   WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
     AND NVL(A.ESTADO, '0') <> '9'
      AND NVL(A.ORIGEN, '0') <> 'A'
      AND M.TP_ART IN ('T', 'S')
   GROUP BY NVL(F.DESCRIPCION, 'SIN FAMILIA')
   ORDER BY KILOS DESC
) WHERE ROWNUM <= :P_TOP";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value  = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value  = fechaFin.Date;
                cmd.Parameters.Add("P_TOP",    OracleDbType.Int32).Value = top;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgTopHiladoKgDto
                    {
                        Familia = GetStr(reader, "FAMILIA"),
                        Kilos   = GetDec(reader, "KILOS")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Top Hilados por KG (Dashboard Gerencial)");
            }

            return result;
        }
    }
}
