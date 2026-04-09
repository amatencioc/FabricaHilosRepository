using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class IndicadoresComercialesService : IIndicadoresComercialesService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndicadoresComercialesService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public IndicadoresComercialesService(
            IConfiguration configuration,
            ILogger<IndicadoresComercialesService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _configuration       = configuration;
            _logger              = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetOracleConnectionString()
        {
            var oraUser  = _httpContextAccessor.HttpContext?.Session.GetString("OracleUser");
            var oraPass  = _httpContextAccessor.HttpContext?.Session.GetString("OraclePass");
            var baseConn = _configuration.GetConnectionString("OracleConnection") ?? string.Empty;

            if (!string.IsNullOrEmpty(oraUser) && !string.IsNullOrEmpty(oraPass))
            {
                var csb = new OracleConnectionStringBuilder(baseConn)
                {
                    UserID   = oraUser,
                    Password = oraPass
                };
                return csb.ToString();
            }

            return baseConn;
        }

        private static string? GetStr(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : r[col]?.ToString();

        private static decimal GetDec(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

        private static int GetInt(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);

        // ─────────────────────────────────────────────────────────
        // Query 1: Importe por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<ImporteAsesorMesDto>> ObtenerImportePorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<ImporteAsesorMesDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT A.VENDEDOR                       COD_ASESOR,
       A.ASESOR,
       A.MES,
       (A.MONTO - NVL(B.MONTO, 0))     IMPORTE
  FROM (SELECT A.VENDEDOR,
               T.DESCRIPCION                   ASESOR,
               TO_CHAR(A.FECHA, 'YYYY/MM')     MES,
               SUM(DECODE(:P_MON,
                          'S', SOLES_SINANT,
                               DOLARES_SINANT)) MONTO
          FROM V_DOCUVEN A, TABLAS_AUXILIARES T
         WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND T.TIPO(+) = 29
           AND T.CODIGO(+) = A.VENDEDOR
         GROUP BY A.VENDEDOR,
                  T.DESCRIPCION,
                  TO_CHAR(A.FECHA, 'YYYY/MM')) A,
       (SELECT D.COD_VENDE                     VENDEDOR,
               TO_CHAR(D.FECHA, 'YYYY/MM')     MES,
               SUM(DECODE(:P_MON,
                          'S',
                          DECODE(D.MONEDA,
                                 'S', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                          DECODE(D.MONEDA,
                                 'D', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
          FROM DOCUVENT D, ITEMDOCU I
         WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND D.ESTADO <> '9'
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE = D.SERIE
           AND I.NUMERO = D.NUMERO
           AND I.COD_ART IN ('9300049997', '9300049999',
                             '930004999A', '9300049998')
         GROUP BY D.COD_VENDE,
                  TO_CHAR(D.FECHA, 'YYYY/MM')) B
 WHERE B.VENDEDOR(+) = A.VENDEDOR
   AND B.MES(+) = A.MES
 ORDER BY A.ASESOR, A.MES";

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
                    result.Add(new ImporteAsesorMesDto
                    {
                        CodAsesor = GetStr(reader, "COD_ASESOR"),
                        Asesor    = GetStr(reader, "ASESOR"),
                        Mes       = GetStr(reader, "MES"),
                        Importe   = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Importe por Asesor/Mes");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 1.1: Detalle de Importe por Cliente por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<DetalleImporteAsesorMesDto>> ObtenerDetalleImportePorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor, string mes)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DetalleImporteAsesorMesDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT A.ASESOR,
       A.MES,
       A.COD_CLIENTE,
       X.RUC,
       X.NOMBRE                          RAZON_SOCIAL,
       (A.MONTO - NVL(B.MONTO, 0))      IMPORTE
  FROM (SELECT A.VENDEDOR,
               T.DESCRIPCION                   ASESOR,
               TO_CHAR(A.FECHA, 'YYYY/MM')     MES,
               A.COD_CLIENTE,
               SUM(DECODE(:P_MON,
                          'S', SOLES_SINANT,
                               DOLARES_SINANT)) MONTO
          FROM V_DOCUVEN A, TABLAS_AUXILIARES T
         WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND T.TIPO(+) = 29
           AND T.CODIGO(+) = A.VENDEDOR
           AND T.DESCRIPCION = :P_ASESOR
           AND TO_CHAR(A.FECHA, 'YYYY/MM') = :P_MES
         GROUP BY A.VENDEDOR,
                  T.DESCRIPCION,
                  TO_CHAR(A.FECHA, 'YYYY/MM'),
                  A.COD_CLIENTE) A,
       (SELECT D.COD_VENDE                     VENDEDOR,
               TO_CHAR(D.FECHA, 'YYYY/MM')     MES,
               D.COD_CLIENTE,
               SUM(DECODE(:P_MON,
                          'S',
                          DECODE(D.MONEDA,
                                 'S', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                          DECODE(D.MONEDA,
                                 'D', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
          FROM DOCUVENT D, ITEMDOCU I
         WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND D.ESTADO <> '9'
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE = D.SERIE
           AND I.NUMERO = D.NUMERO
           AND I.COD_ART IN ('9300049997', '9300049999',
                             '930004999A', '9300049998')
         GROUP BY D.COD_VENDE,
                  TO_CHAR(D.FECHA, 'YYYY/MM'),
                  D.COD_CLIENTE) B,
       CLIENTES X
 WHERE B.VENDEDOR(+) = A.VENDEDOR
   AND B.COD_CLIENTE(+) = A.COD_CLIENTE
   AND B.MES(+) = A.MES
   AND X.COD_CLIENTE = A.COD_CLIENTE
 ORDER BY IMPORTE DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;
                cmd.Parameters.Add("P_ASESOR", OracleDbType.Varchar2).Value = asesor;
                cmd.Parameters.Add("P_MES",    OracleDbType.Varchar2).Value = mes;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DetalleImporteAsesorMesDto
                    {
                        Asesor      = GetStr(reader, "ASESOR"),
                        Mes         = GetStr(reader, "MES"),
                        CodCliente  = GetStr(reader, "COD_CLIENTE"),
                        Ruc         = GetStr(reader, "RUC"),
                        RazonSocial = GetStr(reader, "RAZON_SOCIAL"),
                        Importe     = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Detalle Importe por Asesor/Mes");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 2: Cantidad KG por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<CantidadKgAsesorMesDto>> ObtenerCantidadKgPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<CantidadKgAsesorMesDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT T.DESCRIPCION                    ASESOR,
       TO_CHAR(C.FECHA, 'YYYY/MM')     MES,
       SUM(B.CANTIDAD * E.FACTOR)       CANTIDAD_KG
  FROM ITEMDOCU         B,
       DOCUVENT         C,
       ARTICUL          A,
       TABLAS_AUXILIARES T,
       EQUIVALENCIA     E
 WHERE C.TIPODOC = B.TIPODOC
   AND C.SERIE = B.SERIE
   AND C.NUMERO = B.NUMERO
   AND C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND C.ESTADO <> '9'
   AND A.TP_ART IN ('T', 'S')
   AND A.COD_ART = B.COD_ART
   AND T.TIPO = 29
   AND T.CODIGO = C.COD_VENDE
   AND E.UNIDAD = 'KG'
   AND E.COD_ART = A.COD_ART
 GROUP BY T.DESCRIPCION,
          TO_CHAR(C.FECHA, 'YYYY/MM')
 ORDER BY 1, 2";

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
                    result.Add(new CantidadKgAsesorMesDto
                    {
                        Asesor     = GetStr(reader, "ASESOR"),
                        Mes        = GetStr(reader, "MES"),
                        CantidadKg = GetDec(reader, "CANTIDAD_KG")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Cantidad KG por Asesor/Mes");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 3: Nro. de Clientes por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<NroClientesAsesorMesDto>> ObtenerNroClientesPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<NroClientesAsesorMesDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT T.DESCRIPCION                    ASESOR,
       TO_CHAR(C.FECHA, 'YYYY/MM')     MES,
       COUNT(DISTINCT C.COD_CLIENTE)    NRO_CLIENTES
  FROM DOCUVENT         C,
       ITEMDOCU         B,
       ARTICUL          A,
       TABLAS_AUXILIARES T,
       EQUIVALENCIA     E
 WHERE C.TIPODOC = B.TIPODOC
   AND C.SERIE = B.SERIE
   AND C.NUMERO = B.NUMERO
   AND C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND C.ESTADO <> '9'
   AND A.TP_ART IN ('T', 'S')
   AND A.COD_ART = B.COD_ART
   AND T.TIPO = 29
   AND T.CODIGO = C.COD_VENDE
   AND E.UNIDAD = 'KG'
   AND E.COD_ART = A.COD_ART
 GROUP BY T.DESCRIPCION,
          TO_CHAR(C.FECHA, 'YYYY/MM')
 ORDER BY 1, 2";

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
                    result.Add(new NroClientesAsesorMesDto
                    {
                        Asesor      = GetStr(reader, "ASESOR"),
                        Mes         = GetStr(reader, "MES"),
                        NroClientes = GetInt(reader, "NRO_CLIENTES")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Nro Clientes por Asesor/Mes");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 3.1: Detalle de Clientes por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<DetalleClienteAsesorMesDto>> ObtenerDetalleClientesPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor, string mes)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DetalleClienteAsesorMesDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT V.ASESOR,
       V.MES,
       V.COD_CLIENTE,
       X.RUC,
       X.NOMBRE                              RAZON_SOCIAL,
       NVL(K.CANTIDAD_KG, 0)                 CANTIDAD_KG,
       (V.MONTO - NVL(I.MONTO, 0))           IMPORTE
  FROM (SELECT A.VENDEDOR,
               T.DESCRIPCION                   ASESOR,
               TO_CHAR(A.FECHA, 'YYYY/MM')     MES,
               A.COD_CLIENTE,
               SUM(DECODE(:P_MON,
                          'S', SOLES_SINANT,
                               DOLARES_SINANT)) MONTO
          FROM V_DOCUVEN A, TABLAS_AUXILIARES T
         WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND T.TIPO(+)   = 29
           AND T.CODIGO(+) = A.VENDEDOR
           AND T.DESCRIPCION = :P_ASESOR
           AND TO_CHAR(A.FECHA, 'YYYY/MM') = :P_MES
         GROUP BY A.VENDEDOR,
                  T.DESCRIPCION,
                  TO_CHAR(A.FECHA, 'YYYY/MM'),
                  A.COD_CLIENTE) V,
       (SELECT D.COD_VENDE                     VENDEDOR,
               TO_CHAR(D.FECHA, 'YYYY/MM')     MES,
               D.COD_CLIENTE,
               SUM(DECODE(:P_MON,
                          'S',
                          DECODE(D.MONEDA,
                                 'S', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                          DECODE(D.MONEDA,
                                 'D', I.IMP_VVTA,
                                 ROUND(I.IMP_VVTA / D.IMPORT_CAM, 2)))) MONTO
          FROM DOCUVENT D, ITEMDOCU I
         WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND D.ESTADO <> '9'
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE   = D.SERIE
           AND I.NUMERO  = D.NUMERO
           AND I.COD_ART IN ('9300049997', '9300049999',
                             '930004999A', '9300049998')
         GROUP BY D.COD_VENDE,
                  TO_CHAR(D.FECHA, 'YYYY/MM'),
                  D.COD_CLIENTE) I,
       (SELECT C.COD_VENDE                   VENDEDOR,
               TO_CHAR(C.FECHA, 'YYYY/MM')   MES,
               C.COD_CLIENTE,
               SUM(B.CANTIDAD * E.FACTOR)    CANTIDAD_KG
          FROM DOCUVENT         C,
               ITEMDOCU         B,
               ARTICUL          A,
               EQUIVALENCIA     E
         WHERE C.TIPODOC = B.TIPODOC
           AND C.SERIE   = B.SERIE
           AND C.NUMERO  = B.NUMERO
           AND C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND C.ESTADO <> '9'
           AND A.TP_ART IN ('T', 'S')
           AND A.COD_ART = B.COD_ART
           AND E.UNIDAD  = 'KG'
           AND E.COD_ART = A.COD_ART
         GROUP BY C.COD_VENDE,
                  TO_CHAR(C.FECHA, 'YYYY/MM'),
                  C.COD_CLIENTE) K,
       CLIENTES X
 WHERE I.VENDEDOR(+)    = V.VENDEDOR
   AND I.COD_CLIENTE(+) = V.COD_CLIENTE
   AND I.MES(+)         = V.MES
   AND K.VENDEDOR(+)    = V.VENDEDOR
   AND K.COD_CLIENTE(+) = V.COD_CLIENTE
   AND K.MES(+)         = V.MES
   AND X.COD_CLIENTE    = V.COD_CLIENTE
 ORDER BY IMPORTE DESC, CANTIDAD_KG DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;
                cmd.Parameters.Add("P_ASESOR", OracleDbType.Varchar2).Value = asesor;
                cmd.Parameters.Add("P_MES",    OracleDbType.Varchar2).Value = mes;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DetalleClienteAsesorMesDto
                    {
                        Asesor      = GetStr(reader, "ASESOR"),
                        Mes         = GetStr(reader, "MES"),
                        CodCliente  = GetStr(reader, "COD_CLIENTE"),
                        Ruc         = GetStr(reader, "RUC"),
                        RazonSocial = GetStr(reader, "RAZON_SOCIAL"),
                        CantidadKg  = GetDec(reader, "CANTIDAD_KG"),
                        Importe     = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Detalle Clientes por Asesor/Mes");
            }

            return result;
        }
    }
}
