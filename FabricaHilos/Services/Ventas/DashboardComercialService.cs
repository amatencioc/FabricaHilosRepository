using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class DashboardComercialService : IDashboardComercialService
    {
        private readonly string _baseConnectionString;
        private readonly ILogger<DashboardComercialService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DashboardComercialService(
            IConfiguration configuration,
            ILogger<DashboardComercialService> logger,
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

        private static int GetInt(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);

        // ─────────────────────────────────────────────────────────
        // Query 1: Importe por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<DcImporteAsesorMesDto>> ObtenerImportePorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DcImporteAsesorMesDto>();
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
                    AND (T.DESCRIPCION IS NULL OR UPPER(T.DESCRIPCION) <> 'OFICINA')
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
                                          ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2)))) MONTO
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
                    result.Add(new DcImporteAsesorMesDto
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
                _logger.LogError(ex, "Error al obtener Importe por Asesor/Mes (Dashboard Comercial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 1.1: Detalle de Importe por Cliente por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<DcDetalleImporteAsesorMesDto>> ObtenerDetalleImportePorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor, string mes)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DcDetalleImporteAsesorMesDto>();
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
           AND T.TIPO = 29
           AND T.CODIGO = A.VENDEDOR
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
                                 ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2)))) MONTO
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
                    result.Add(new DcDetalleImporteAsesorMesDto
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
                _logger.LogError(ex, "Error al obtener Detalle Importe por Asesor/Mes (Dashboard Comercial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 2: Cantidad KG por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<DcCantidadKgAsesorMesDto>> ObtenerCantidadKgPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DcCantidadKgAsesorMesDto>();
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
   AND UPPER(T.DESCRIPCION) <> 'OFICINA'
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
                    result.Add(new DcCantidadKgAsesorMesDto
                    {
                        Asesor     = GetStr(reader, "ASESOR"),
                        Mes        = GetStr(reader, "MES"),
                        CantidadKg = GetDec(reader, "CANTIDAD_KG")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Cantidad KG por Asesor/Mes (Dashboard Comercial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 3: Nro. de Clientes por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<DcNroClientesAsesorMesDto>> ObtenerNroClientesPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DcNroClientesAsesorMesDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT T.DESCRIPCION                    ASESOR,
       COUNT(DISTINCT C.COD_CLIENTE)    NRO_CLIENTES
  FROM DOCUVENT         C,
       ITEMDOCU         B,
       ARTICUL          A,
       TABLAS_AUXILIARES T
 WHERE C.TIPODOC = B.TIPODOC
   AND C.SERIE   = B.SERIE
   AND C.NUMERO  = B.NUMERO
   AND C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
   AND C.ESTADO <> '9'
   AND A.TP_ART IN ('T', 'S')
   AND A.COD_ART = B.COD_ART
   AND T.TIPO    = 29
   AND T.CODIGO  = C.COD_VENDE
   AND UPPER(T.DESCRIPCION) <> 'OFICINA'
 GROUP BY T.DESCRIPCION
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
                    result.Add(new DcNroClientesAsesorMesDto
                    {
                        Asesor      = GetStr(reader, "ASESOR"),
                        Mes         = null,
                        NroClientes = GetInt(reader, "NRO_CLIENTES")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Nro Clientes por Asesor/Mes (Dashboard Comercial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 3.1: Detalle de Clientes por Asesor / Mes
        // ─────────────────────────────────────────────────────────
        public async Task<List<DcDetalleClienteAsesorMesDto>> ObtenerDetalleClientesPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor, string mes)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DcDetalleClienteAsesorMesDto>();
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
           AND T.TIPO   = 29
           AND T.CODIGO = A.VENDEDOR
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
                                 ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2)))) MONTO
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
                    result.Add(new DcDetalleClienteAsesorMesDto
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
                _logger.LogError(ex, "Error al obtener Detalle Clientes por Asesor/Mes (Dashboard Comercial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 4: Top N clientes por Asesor (Kilos e Importe)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DcTopClienteAsesorDto>> ObtenerTopClientesPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DcTopClienteAsesorDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            // Se obtienen todos los clientes por asesor con kilos e importe, agrupado por año
            const string sql = @"
SELECT V.ASESOR,
       X.NOMBRE                              RAZON_SOCIAL,
       NVL(K.CANTIDAD_KG, 0)                 CANTIDAD_KG,
       (V.MONTO - NVL(INF.MONTO, 0))        IMPORTE,
       V.ANIO
  FROM (SELECT A.VENDEDOR,
               T.DESCRIPCION                   ASESOR,
               A.COD_CLIENTE,
               EXTRACT(YEAR FROM A.FECHA)       ANIO,
               SUM(DECODE(:P_MON,
                           'S', SOLES_SINANT,
                                DOLARES_SINANT)) MONTO
            FROM V_DOCUVEN A, TABLAS_AUXILIARES T
          WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
            AND T.TIPO(+)   = 29
            AND T.CODIGO(+) = A.VENDEDOR
            AND (T.DESCRIPCION IS NULL OR UPPER(T.DESCRIPCION) <> 'OFICINA')
          GROUP BY A.VENDEDOR,
                   T.DESCRIPCION,
                   A.COD_CLIENTE,
                   EXTRACT(YEAR FROM A.FECHA)) V,
       (SELECT D.COD_VENDE                     VENDEDOR,
               D.COD_CLIENTE,
               EXTRACT(YEAR FROM D.FECHA)       ANIO,
               SUM(DECODE(:P_MON,
                           'S',
                           DECODE(D.MONEDA,
                                  'S', I.IMP_VVTA,
                                  ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                           DECODE(D.MONEDA,
                                  'D', I.IMP_VVTA,
                                  ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2)))) MONTO
          FROM DOCUVENT D, ITEMDOCU I
         WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
           AND D.ESTADO <> '9'
           AND I.TIPODOC = D.TIPODOC
           AND I.SERIE   = D.SERIE
           AND I.NUMERO  = D.NUMERO
           AND I.COD_ART IN ('9300049997', '9300049999',
                             '930004999A', '9300049998')
         GROUP BY D.COD_VENDE,
                  D.COD_CLIENTE,
                  EXTRACT(YEAR FROM D.FECHA)) INF,
       (SELECT C.COD_VENDE                   VENDEDOR,
               C.COD_CLIENTE,
               EXTRACT(YEAR FROM C.FECHA)    ANIO,
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
                  C.COD_CLIENTE,
                  EXTRACT(YEAR FROM C.FECHA)) K,
       CLIENTES X
 WHERE INF.VENDEDOR(+)    = V.VENDEDOR
   AND INF.COD_CLIENTE(+) = V.COD_CLIENTE
   AND INF.ANIO(+)        = V.ANIO
   AND K.VENDEDOR(+)      = V.VENDEDOR
   AND K.COD_CLIENTE(+)   = V.COD_CLIENTE
   AND K.ANIO(+)          = V.ANIO
   AND X.COD_CLIENTE      = V.COD_CLIENTE
 ORDER BY V.ASESOR, V.ANIO, (V.MONTO - NVL(INF.MONTO, 0)) DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                var allRows = new List<DcTopClienteAsesorDto>();
                while (await reader.ReadAsync())
                {
                    allRows.Add(new DcTopClienteAsesorDto
                    {
                        Asesor      = GetStr(reader, "ASESOR"),
                        RazonSocial = GetStr(reader, "RAZON_SOCIAL"),
                        CantidadKg  = GetDec(reader, "CANTIDAD_KG"),
                        Importe     = GetDec(reader, "IMPORTE"),
                        Anio        = GetInt(reader, "ANIO")
                    });
                }

                // Excluir filas sin asesor (vendedores sin registro en TABLAS_AUXILIARES)
                // y aplicar TOP N separado: por importe y por KG
                var known = allRows.Where(r => r.Asesor != null).ToList();

                var topImporte = known
                    .GroupBy(r => new { r.Asesor, r.Anio })
                    .SelectMany(g => g.OrderByDescending(r => r.Importe).Take(top).Select(r => { r.TopType = "importe"; return r; }))
                    .ToList();

                var topKg = known
                    .GroupBy(r => new { r.Asesor, r.Anio })
                    .SelectMany(g => g.OrderByDescending(r => r.CantidadKg).Take(top).Select(r => { r.TopType = "kg"; return r; }))
                    .ToList();

                // Marcar "both" los que aparecen en ambos rankings
                var keysBoth = topImporte
                    .Select(r => (r.Asesor, r.Anio, r.RazonSocial))
                    .Intersect(topKg.Select(r => (r.Asesor, r.Anio, r.RazonSocial)))
                    .ToHashSet();

                result = topImporte
                    .Union(topKg)
                    .DistinctBy(r => (r.Asesor, r.Anio, r.RazonSocial))
                    .Select(r =>
                    {
                        if (keysBoth.Contains((r.Asesor, r.Anio, r.RazonSocial))) r.TopType = "both";
                        return r;
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Top Clientes por Asesor (Dashboard Comercial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 5: Clientes del Asesor — Importe + Giro (período completo)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DcClienteImporteAsesorDto>> ObtenerClientesImportePorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DcClienteImporteAsesorDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

                                                 const string sql = @"
                                     SELECT BASE.COD_CLIENTE,
                                            X.RUC,
                                            X.NOMBRE                            RAZON_SOCIAL,
                                            NVL(T2.ABREVIADA, 'SIN GIRO')       GIRO,
                                            (NVL(IMP.MONTO, 0) - NVL(INAF.MONTO, 0)) IMPORTE
                                       FROM (SELECT DISTINCT C.COD_CLIENTE
                                               FROM DOCUVENT         C,
                                                    TABLAS_AUXILIARES T
                                              WHERE C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                                                AND C.ESTADO <> '9'
                                                AND T.TIPO    = 29
                                                AND T.CODIGO  = C.COD_VENDE
                                                AND T.DESCRIPCION = :P_ASESOR) BASE,
                                            (SELECT A.COD_CLIENTE,
                                                    SUM(DECODE(:P_MON,
                                                               'S', SOLES_SINANT,
                                                                    DOLARES_SINANT)) MONTO
                                               FROM V_DOCUVEN A, TABLAS_AUXILIARES T
                                              WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                                                AND T.TIPO = 29
                                                AND T.CODIGO = A.VENDEDOR
                                                AND T.DESCRIPCION = :P_ASESOR
                                              GROUP BY A.COD_CLIENTE) IMP,
                                            (SELECT D.COD_CLIENTE,
                                                    SUM(DECODE(:P_MON,
                                                               'S',
                                                               DECODE(D.MONEDA,
                                                                      'S', I.IMP_VVTA,
                                                                      ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                                                               DECODE(D.MONEDA,
                                                                      'D', I.IMP_VVTA,
                                                                      ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2)))) MONTO
                                               FROM DOCUVENT D, ITEMDOCU I, TABLAS_AUXILIARES T
                                              WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                                                AND D.ESTADO <> '9'
                                                AND I.TIPODOC = D.TIPODOC
                                                AND I.SERIE   = D.SERIE
                                                AND I.NUMERO  = D.NUMERO
                                                AND I.COD_ART IN ('9300049997', '9300049999',
                                                                  '930004999A', '9300049998')
                                                AND T.TIPO = 29
                                                AND T.CODIGO = D.COD_VENDE
                                                AND T.DESCRIPCION = :P_ASESOR
                                              GROUP BY D.COD_CLIENTE) INAF,
                                            CLIENTES X,
                                            (SELECT CODIGO, MAX(ABREVIADA) ABREVIADA
                                               FROM TABLAS_AUXILIARES
                                              WHERE TIPO = 27
                                              GROUP BY CODIGO) T2
                                      WHERE IMP.COD_CLIENTE(+)  = BASE.COD_CLIENTE
                                        AND INAF.COD_CLIENTE(+) = BASE.COD_CLIENTE
                                        AND X.COD_CLIENTE       = BASE.COD_CLIENTE
                                        AND T2.CODIGO(+)        = X.GIRO
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

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DcClienteImporteAsesorDto
                    {
                        CodCliente  = GetStr(reader, "COD_CLIENTE"),
                        Ruc         = GetStr(reader, "RUC"),
                        RazonSocial = GetStr(reader, "RAZON_SOCIAL"),
                        Giro        = GetStr(reader, "GIRO"),
                        Importe     = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Clientes Importe por Asesor (Dashboard Comercial)");
            }

            return result;
        }

        /* ── Query 5b: Todos los Clientes — Importe + Giro (todos los asesores) ── */
        public async Task<List<DcClienteImporteTodosDto>> ObtenerClientesImporteTodosAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DcClienteImporteTodosDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

                                                 const string sql = @"
                                     SELECT BASE.COD_CLIENTE,
                                            BASE.ASESOR,
                                            X.RUC,
                                            X.NOMBRE                            RAZON_SOCIAL,
                                            NVL(T2.ABREVIADA, 'SIN GIRO')       GIRO,
                                            (NVL(IMP.MONTO, 0) - NVL(INAF.MONTO, 0)) IMPORTE
                                       FROM (SELECT DISTINCT C.COD_CLIENTE,
                                                    T.DESCRIPCION ASESOR
                                               FROM DOCUVENT         C,
                                                    TABLAS_AUXILIARES T
                                              WHERE C.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                                                AND C.ESTADO <> '9'
                                                AND T.TIPO    = 29
                                                AND T.CODIGO  = C.COD_VENDE
                                                AND UPPER(T.DESCRIPCION) <> 'OFICINA') BASE,
                                            (SELECT A.COD_CLIENTE,
                                                    T.DESCRIPCION ASESOR,
                                                    SUM(DECODE(:P_MON,
                                                               'S', SOLES_SINANT,
                                                                    DOLARES_SINANT)) MONTO
                                               FROM V_DOCUVEN A, TABLAS_AUXILIARES T
                                              WHERE A.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                                                AND T.TIPO = 29
                                                AND T.CODIGO = A.VENDEDOR
                                                AND UPPER(T.DESCRIPCION) <> 'OFICINA'
                                              GROUP BY A.COD_CLIENTE, T.DESCRIPCION) IMP,
                                            (SELECT D.COD_CLIENTE,
                                                    T.DESCRIPCION ASESOR,
                                                    SUM(DECODE(:P_MON,
                                                               'S',
                                                               DECODE(D.MONEDA,
                                                                      'S', I.IMP_VVTA,
                                                                      ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)),
                                                               DECODE(D.MONEDA,
                                                                      'D', I.IMP_VVTA,
                                                                      ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2)))) MONTO
                                               FROM DOCUVENT D, ITEMDOCU I, TABLAS_AUXILIARES T
                                              WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                                                AND D.ESTADO <> '9'
                                                AND I.TIPODOC = D.TIPODOC
                                                AND I.SERIE   = D.SERIE
                                                AND I.NUMERO  = D.NUMERO
                                                AND I.COD_ART IN ('9300049997', '9300049999',
                                                                  '930004999A', '9300049998')
                                                AND T.TIPO = 29
                                                AND T.CODIGO = D.COD_VENDE
                                                AND UPPER(T.DESCRIPCION) <> 'OFICINA'
                                              GROUP BY D.COD_CLIENTE, T.DESCRIPCION) INAF,
                                            CLIENTES X,
                                            (SELECT CODIGO, MAX(ABREVIADA) ABREVIADA
                                               FROM TABLAS_AUXILIARES
                                              WHERE TIPO = 27
                                              GROUP BY CODIGO) T2
                                      WHERE IMP.COD_CLIENTE(+)  = BASE.COD_CLIENTE
                                        AND IMP.ASESOR(+)       = BASE.ASESOR
                                        AND INAF.COD_CLIENTE(+) = BASE.COD_CLIENTE
                                        AND INAF.ASESOR(+)      = BASE.ASESOR
                                        AND X.COD_CLIENTE       = BASE.COD_CLIENTE
                                        AND T2.CODIGO(+)        = X.GIRO
                                      ORDER BY BASE.ASESOR, IMPORTE DESC";

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
                    result.Add(new DcClienteImporteTodosDto
                    {
                        Asesor      = GetStr(reader, "ASESOR"),
                        CodCliente  = GetStr(reader, "COD_CLIENTE"),
                        Ruc         = GetStr(reader, "RUC"),
                        RazonSocial = GetStr(reader, "RAZON_SOCIAL"),
                        Giro        = GetStr(reader, "GIRO"),
                        Importe     = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Clientes Importe Todos (Dashboard Comercial)");
            }

            return result;
        }
    }
}
