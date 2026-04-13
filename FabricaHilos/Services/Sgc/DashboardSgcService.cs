using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    public class DashboardSgcService : IDashboardSgcService
    {
        private readonly string _baseConnectionString;
        private readonly ILogger<DashboardSgcService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DashboardSgcService(
            IConfiguration configuration,
            ILogger<DashboardSgcService> logger,
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
        // Query 1 — KPI global
        // ─────────────────────────────────────────────────────────
        public async Task<DashKpiDto> ObtenerKpiAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new DashKpiDto();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT
    COUNT(*)                                                                      AS TOTAL_PEDIDOS,
    NVL(SUM(P.TOTAL_PEDIDO), 0)                                                  AS TOTAL_PEDIDO,
    NVL(SUM(CASE WHEN P.ESTADO IN ('0','1','5') THEN P.TOTAL_PEDIDO ELSE 0 END), 0) AS TOTAL_PENDIENTE
FROM SIG.PEDIDO P
WHERE P.ESTADO <> '9'
  AND P.FECHA >= :fechaInicio
  AND P.FECHA <  :fechaFin + 1";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.TotalPedidos    = GetInt(reader, "TOTAL_PEDIDOS");
                    result.TotalPedido     = GetDec(reader, "TOTAL_PEDIDO");
                    result.TotalPendiente  = GetDec(reader, "TOTAL_PENDIENTE");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerKpiAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 2 — Por Estado
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashEstadoDto>> ObtenerPorEstadoAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashEstadoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT P.ESTADO, COUNT(*) AS CANTIDAD, NVL(SUM(P.TOTAL_PEDIDO), 0) AS TOTAL
FROM SIG.PEDIDO P
WHERE P.ESTADO IN ('5', '6')
  AND P.FECHA >= :fechaInicio
  AND P.FECHA <  :fechaFin + 1
GROUP BY P.ESTADO
ORDER BY CANTIDAD DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var estado = GetStr(reader, "ESTADO") ?? string.Empty;
                    result.Add(new DashEstadoDto
                    {
                        Estado     = estado,
                        DescEstado = DescripcionEstado(estado),
                        Cantidad   = GetInt(reader, "CANTIDAD"),
                        Total      = GetDec(reader, "TOTAL")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerPorEstadoAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 3 — Evolución mensual
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashEvolucionDto>> ObtenerEvolucionMensualAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashEvolucionDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT
    TO_CHAR(TRUNC(P.FECHA, 'MM'), 'YYYY-MM') AS MES,
    P.MONEDA,
    COUNT(*)                                   AS NUM_PEDIDOS,
    NVL(SUM(P.TOTAL_PEDIDO),    0)             AS TOTAL_PEDIDO
FROM SIG.PEDIDO P
WHERE P.ESTADO <> '9'
  AND P.FECHA >= :fechaInicio
  AND P.FECHA <  :fechaFin + 1
GROUP BY TRUNC(P.FECHA, 'MM'), P.MONEDA
ORDER BY TRUNC(P.FECHA, 'MM'), P.MONEDA";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashEvolucionDto
                    {
                        Mes         = GetStr(reader, "MES"),
                        Moneda      = GetStr(reader, "MONEDA"),
                        NumPedidos  = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido = GetDec(reader, "TOTAL_PEDIDO")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerEvolucionMensualAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 4 — Top Clientes (ROWNUM, Oracle 10g)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashTopClienteDto>> ObtenerTopClientesAsync(
            DateTime fechaInicio, DateTime fechaFin, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashTopClienteDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT * FROM (
    SELECT P.COD_CLIENTE, P.NOMBRE,
           COUNT(*)                        AS NUM_PEDIDOS,
           NVL(SUM(P.TOTAL_PEDIDO),    0)  AS TOTAL_PEDIDO,
           NVL(SUM(KG_AGG.PESO_GUIAS), 0) AS PESO_TOTAL
    FROM SIG.PEDIDO P
    LEFT JOIN (
        SELECT TRIM(KG.NRO_DOC_REF)        AS NUM_PED_REF,
               TRIM(KG.SER_DOC_REF)        AS SER_REF,
               KG.TIP_DOC_REF,
               NVL(SUM(KG.PESO_TOTAL), 0)  AS PESO_GUIAS
        FROM SIG.KARDEX_G KG
        GROUP BY TRIM(KG.NRO_DOC_REF), TRIM(KG.SER_DOC_REF), KG.TIP_DOC_REF
    ) KG_AGG ON TO_CHAR(P.NUM_PED) = KG_AGG.NUM_PED_REF
           AND TO_CHAR(P.SERIE)   = KG_AGG.SER_REF
           AND P.TIPO_DOCTO       = KG_AGG.TIP_DOC_REF
    WHERE P.ESTADO <> '9'
      AND P.FECHA >= :fechaInicio
      AND P.FECHA <  :fechaFin + 1
    GROUP BY P.COD_CLIENTE, P.NOMBRE
    ORDER BY TOTAL_PEDIDO DESC
) WHERE ROWNUM <= :top";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("top",         OracleDbType.Int32).Value = top;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashTopClienteDto
                    {
                        CodCliente  = GetStr(reader, "COD_CLIENTE"),
                        Nombre      = GetStr(reader, "NOMBRE"),
                        NumPedidos  = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido = GetDec(reader, "TOTAL_PEDIDO"),
                        PesoTotal   = GetDec(reader, "PESO_TOTAL")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerTopClientesAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 5 — Top Artículos (ROWNUM, Oracle 10g)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashTopArticuloDto>> ObtenerTopArticulosAsync(
            DateTime fechaInicio, DateTime fechaFin, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashTopArticuloDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT * FROM (
    SELECT I.COD_ART, A.DESCRIPCION,
           NVL(SUM(I.CANTIDAD), 0)                                          AS CANT_PEDIDA,
           NVL(SUM(I.CANTIDAD - NVL(I.SALDO, I.CANTIDAD)), 0)               AS CANT_DESPACHADA,
           NVL(SUM(NVL(I.SALDO, 0)), 0)                                     AS CANT_PENDIENTE,
           NVL(SUM(I.PRECIO * I.CANTIDAD), 0)                               AS VALOR_TOTAL
    FROM SIG.ITEMPED I
    LEFT JOIN SIG.ARTICUL A ON A.COD_ART = I.COD_ART
    INNER JOIN SIG.PEDIDO P ON P.NUM_PED = I.NUM_PED AND P.SERIE = I.SERIE
    WHERE P.ESTADO <> '9'
      AND P.FECHA >= :fechaInicio
      AND P.FECHA <  :fechaFin + 1
    GROUP BY I.COD_ART, A.DESCRIPCION
    ORDER BY CANT_PEDIDA DESC
) WHERE ROWNUM <= :top";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("top",         OracleDbType.Int32).Value = top;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashTopArticuloDto
                    {
                        CodArt         = GetStr(reader, "COD_ART"),
                        Descripcion    = GetStr(reader, "DESCRIPCION"),
                        CantPedida     = GetDec(reader, "CANT_PEDIDA"),
                        CantDespachada = GetDec(reader, "CANT_DESPACHADA"),
                        CantPendiente  = GetDec(reader, "CANT_PENDIENTE"),
                        ValorTotal     = GetDec(reader, "VALOR_TOTAL")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerTopArticulosAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 6 — Vendedores con nombre (JOIN TABLAS_AUXILIARES TIPO=29)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashVendedorDto>> ObtenerPorVendedorAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashVendedorDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT VV.COD_VENDE, VV.NOMBRE_VENDEDOR, VV.MONEDA,
       VV.NUM_PEDIDOS, VV.TOTAL_PEDIDO
FROM (
    SELECT P.COD_VENDE,
           NVL(TA.DESCRIPCION, P.COD_VENDE)   AS NOMBRE_VENDEDOR,
           P.MONEDA,
           COUNT(*)                             AS NUM_PEDIDOS,
           NVL(SUM(P.TOTAL_PEDIDO), 0)         AS TOTAL_PEDIDO,
           NVL(SUM(SUM(P.TOTAL_PEDIDO)) OVER (PARTITION BY P.COD_VENDE), 0) AS TOT_VENDE
    FROM SIG.PEDIDO P
    LEFT JOIN SIG.TABLAS_AUXILIARES TA
           ON TA.TIPO   = 29
          AND TA.CODIGO = P.COD_VENDE
    WHERE P.ESTADO <> '9'
      AND P.FECHA >= :fechaInicio
      AND P.FECHA <  :fechaFin + 1
    GROUP BY P.COD_VENDE, TA.DESCRIPCION, P.MONEDA
) VV
ORDER BY TOT_VENDE DESC, VV.MONEDA";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashVendedorDto
                    {
                        CodVende       = GetStr(reader, "COD_VENDE"),
                        NombreVendedor = GetStr(reader, "NOMBRE_VENDEDOR"),
                        Moneda         = GetStr(reader, "MONEDA"),
                        NumPedidos     = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido    = GetDec(reader, "TOTAL_PEDIDO")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerPorVendedorAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 7 — Por Moneda
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashMonedaDto>> ObtenerPorMonedaAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashMonedaDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT P.MONEDA, COUNT(*) AS NUM_PEDIDOS, NVL(SUM(P.TOTAL_PEDIDO), 0) AS TOTAL_PEDIDO
FROM SIG.PEDIDO P
WHERE P.ESTADO <> '9'
  AND P.FECHA >= :fechaInicio
  AND P.FECHA <  :fechaFin + 1
GROUP BY P.MONEDA
ORDER BY TOTAL_PEDIDO DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashMonedaDto
                    {
                        Moneda     = GetStr(reader, "MONEDA"),
                        NumPedidos = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido = GetDec(reader, "TOTAL_PEDIDO")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerPorMonedaAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 8 — Sucursal del cliente (JOIN SIG.SUCURSALES)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashSucursalClienteDto>> ObtenerSucursalClienteAsync(
            DateTime fechaInicio, DateTime fechaFin, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashSucursalClienteDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT * FROM (
    SELECT
        S.NRO_SUCUR,
        NVL(S.NOMBRE_SUCURSAL, 'Sucursal ' || S.NRO_SUCUR) AS NOMBRE_SUCURSAL,
        NVL(S.CIUDAD,   '—') AS CIUDAD,
        NVL(S.DISTRITO, '—') AS DISTRITO,
        COUNT(P.NUM_PED)               AS NUM_PEDIDOS,
        NVL(SUM(P.TOTAL_PEDIDO),    0) AS TOTAL_PEDIDO
    FROM SIG.PEDIDO P
    INNER JOIN SIG.SUCURSALES S
           ON S.COD_CLIENTE = P.COD_CLIENTE
          AND S.NRO_SUCUR   = P.NRO_SUCUR
    WHERE P.ESTADO <> '9'
      AND P.FECHA >= :fechaInicio
      AND P.FECHA <  :fechaFin + 1
    GROUP BY S.NRO_SUCUR, S.NOMBRE_SUCURSAL, S.CIUDAD, S.DISTRITO
    ORDER BY TOTAL_PEDIDO DESC
) WHERE ROWNUM <= :top";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("top",         OracleDbType.Int32).Value = top;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashSucursalClienteDto
                    {
                        NroSucur       = GetStr(reader, "NRO_SUCUR"),
                        NombreSucursal = GetStr(reader, "NOMBRE_SUCURSAL"),
                        Ciudad         = GetStr(reader, "CIUDAD"),
                        Distrito       = GetStr(reader, "DISTRITO"),
                        NumPedidos     = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido    = GetDec(reader, "TOTAL_PEDIDO")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerSucursalClienteAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 9 — Despachos mensuales (SIG.KARDEX_G)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashDespachoDto>> ObtenerDespachosAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashDespachoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT
    TO_CHAR(TRUNC(KG.FCH_TRANSAC, 'MM'), 'YYYY-MM') AS MES,
    COUNT(*)                                           AS NUM_GUIAS,
    NVL(SUM(KG.PESO_TOTAL), 0)                         AS PESO_TOTAL
FROM SIG.KARDEX_G KG
WHERE KG.FCH_TRANSAC >= :fechaInicio
  AND KG.FCH_TRANSAC <  :fechaFin + 1
GROUP BY TRUNC(KG.FCH_TRANSAC, 'MM')
ORDER BY TRUNC(KG.FCH_TRANSAC, 'MM')";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashDespachoDto
                    {
                        Mes       = GetStr(reader, "MES"),
                        NumGuias  = GetInt(reader, "NUM_GUIAS"),
                        PesoTotal = GetDec(reader, "PESO_TOTAL")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerDespachosAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 10 — Pedidos en Riesgo (Emitidos sin avance)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashPedidoRiesgoDto>> ObtenerPedidosEnRiesgoAsync(int diasMinimos = 30)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashPedidoRiesgoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT P.NUM_PED, P.SERIE, P.NOMBRE, P.FECHA,
       TRUNC(SYSDATE) - TRUNC(P.FECHA) AS DIAS_EMITIDO,
       NVL(P.TOTAL_PEDIDO, 0)          AS TOTAL_PEDIDO,
       P.COD_VENDE
FROM SIG.PEDIDO P
WHERE P.ESTADO IN ('0', '1')
  AND TRUNC(SYSDATE) - TRUNC(P.FECHA) > :diasMinimos
ORDER BY DIAS_EMITIDO DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("diasMinimos", OracleDbType.Int32).Value = diasMinimos;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DashPedidoRiesgoDto
                    {
                        NumPed      = GetInt(reader, "NUM_PED"),
                        Serie       = GetInt(reader, "SERIE"),
                        Nombre      = GetStr(reader, "NOMBRE"),
                        Fecha       = reader["FECHA"] == DBNull.Value ? null : Convert.ToDateTime(reader["FECHA"]),
                        DiasEmitido = GetInt(reader, "DIAS_EMITIDO"),
                        TotalPedido = GetDec(reader, "TOTAL_PEDIDO"),
                        CodVende    = GetStr(reader, "COD_VENDE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerPedidosEnRiesgoAsync");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 11 — Ticket Promedio por Cliente
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashTicketClienteDto>> ObtenerTicketPorClienteAsync(
            DateTime fechaInicio, DateTime fechaFin, int top = 15)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashTicketClienteDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT * FROM (
    SELECT P.COD_CLIENTE, P.NOMBRE,
           COUNT(*)                        AS NUM_PEDIDOS,
           NVL(SUM(P.TOTAL_PEDIDO), 0)    AS TOTAL_PEDIDO,
           ROUND(NVL(SUM(P.TOTAL_PEDIDO), 0) / COUNT(*), 2) AS TICKET_PROMEDIO
    FROM SIG.PEDIDO P
    WHERE P.ESTADO <> '9'
      AND P.FECHA >= :fechaInicio
      AND P.FECHA <  :fechaFin + 1
    GROUP BY P.COD_CLIENTE, P.NOMBRE
    ORDER BY TICKET_PROMEDIO DESC
) WHERE ROWNUM <= :top";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value  = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value  = fechaFin.Date;
                cmd.Parameters.Add("top",         OracleDbType.Int32).Value = top;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    result.Add(new DashTicketClienteDto
                    {
                        CodCliente    = GetStr(reader, "COD_CLIENTE"),
                        Nombre        = GetStr(reader, "NOMBRE"),
                        NumPedidos    = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido   = GetDec(reader, "TOTAL_PEDIDO"),
                        TicketPromedio = GetDec(reader, "TICKET_PROMEDIO")
                    });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en ObtenerTicketPorClienteAsync"); }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 12 — Ciclo de Cierre mensual (cycle time)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashCicloDto>> ObtenerCicloCierreAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashCicloDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT
    TO_CHAR(TRUNC(P.FECHA, 'MM'), 'YYYY-MM') AS MES,
    COUNT(P.NUM_PED) AS NUM_PEDIDOS,
    ROUND(AVG(
        CASE WHEN KG_MAX.FCH_MAX IS NOT NULL
             THEN TRUNC(KG_MAX.FCH_MAX) - TRUNC(P.FECHA)
             ELSE NULL END
    ), 1) AS DIAS_PROM_CIERRE
FROM SIG.PEDIDO P
LEFT JOIN (
    SELECT TRIM(KG.NRO_DOC_REF)       AS NUM_PED_REF,
           TRIM(KG.SER_DOC_REF)       AS SER_REF,
           KG.TIP_DOC_REF,
           MAX(KG.FCH_TRANSAC)        AS FCH_MAX
    FROM SIG.KARDEX_G KG
    GROUP BY TRIM(KG.NRO_DOC_REF), TRIM(KG.SER_DOC_REF), KG.TIP_DOC_REF
) KG_MAX ON TO_CHAR(P.NUM_PED) = KG_MAX.NUM_PED_REF
        AND TO_CHAR(P.SERIE)   = KG_MAX.SER_REF
        AND P.TIPO_DOCTO       = KG_MAX.TIP_DOC_REF
WHERE P.ESTADO IN ('5', '6')
  AND P.FECHA >= :fechaInicio
  AND P.FECHA <  :fechaFin + 1
GROUP BY TRUNC(P.FECHA, 'MM')
ORDER BY TRUNC(P.FECHA, 'MM')";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    result.Add(new DashCicloDto
                    {
                        Mes            = GetStr(reader, "MES"),
                        NumPedidos     = GetInt(reader, "NUM_PEDIDOS"),
                        DiasPromCierre = GetDec(reader, "DIAS_PROM_CIERRE")
                    });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en ObtenerCicloCierreAsync"); }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 13 — Tasa de Recompra (frecuencia por cliente)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashRecompraDto>> ObtenerRecompraAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashRecompraDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT
    CASE
        WHEN ORDERS = 1           THEN '1 pedido (Único)'
        WHEN ORDERS <= 3          THEN '2-3 pedidos (Ocasional)'
        WHEN ORDERS <= 6          THEN '4-6 pedidos (Frecuente)'
        ELSE                           '7+ pedidos (Leal)'
    END AS FRECUENCIA,
    COUNT(*)        AS NUM_CLIENTES,
    SUM(ORDERS)     AS NUM_PEDIDOS_TOTAL,
    NVL(SUM(TOTAL), 0) AS TOTAL_PEDIDO
FROM (
    SELECT COD_CLIENTE,
           COUNT(*)                    AS ORDERS,
           NVL(SUM(TOTAL_PEDIDO), 0)  AS TOTAL
    FROM SIG.PEDIDO
    WHERE ESTADO <> '9'
      AND FECHA >= :fechaInicio
      AND FECHA <  :fechaFin + 1
    GROUP BY COD_CLIENTE
)
GROUP BY
    CASE
        WHEN ORDERS = 1     THEN '1 pedido (Único)'
        WHEN ORDERS <= 3    THEN '2-3 pedidos (Ocasional)'
        WHEN ORDERS <= 6    THEN '4-6 pedidos (Frecuente)'
        ELSE                     '7+ pedidos (Leal)'
    END
ORDER BY MIN(ORDERS)";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    result.Add(new DashRecompraDto
                    {
                        Frecuencia       = GetStr(reader, "FRECUENCIA"),
                        NumClientes      = GetInt(reader, "NUM_CLIENTES"),
                        NumPedidosTotal  = GetInt(reader, "NUM_PEDIDOS_TOTAL"),
                        TotalPedido      = GetDec(reader, "TOTAL_PEDIDO")
                    });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en ObtenerRecompraAsync"); }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 14 — Concentración de Riesgo (Pareto por cliente)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashConcentracionDto>> ObtenerConcentracionRiesgoAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashConcentracionDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT
    CASE
        WHEN RN = 1     THEN 'Cliente #1'
        WHEN RN <= 3    THEN 'Top 2-3'
        WHEN RN <= 5    THEN 'Top 4-5'
        WHEN RN <= 10   THEN 'Top 6-10'
        ELSE                 'Resto'
    END AS SEGMENTO,
    COUNT(*) AS NUM_CLIENTES,
    NVL(SUM(TOTAL_PEDIDO), 0) AS TOTAL_PEDIDO
FROM (
    SELECT COD_CLIENTE,
           NVL(SUM(TOTAL_PEDIDO), 0) AS TOTAL_PEDIDO,
           ROW_NUMBER() OVER (ORDER BY NVL(SUM(TOTAL_PEDIDO), 0) DESC) AS RN
    FROM SIG.PEDIDO
    WHERE ESTADO <> '9'
      AND FECHA >= :fechaInicio
      AND FECHA <  :fechaFin + 1
    GROUP BY COD_CLIENTE
) RANKED
GROUP BY
    CASE
        WHEN RN = 1     THEN 'Cliente #1'
        WHEN RN <= 3    THEN 'Top 2-3'
        WHEN RN <= 5    THEN 'Top 4-5'
        WHEN RN <= 10   THEN 'Top 6-10'
        ELSE                 'Resto'
    END
ORDER BY MIN(RN)";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    result.Add(new DashConcentracionDto
                    {
                        Segmento   = GetStr(reader, "SEGMENTO"),
                        NumClientes = GetInt(reader, "NUM_CLIENTES"),
                        TotalPedido = GetDec(reader, "TOTAL_PEDIDO")
                    });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en ObtenerConcentracionRiesgoAsync"); }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 15 — Por Zona / Ciudad (geo-marketing)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DashZonaDto>> ObtenerPorZonaAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DashZonaDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            // Une PEDIDO → CLIENTES por RUC; usa los 2 primeros dígitos de COD_UBC (ubigeo)
            // para agrupar por departamento y decodificar el nombre legible.
            const string sql = @"
SELECT CASE T.DPTO
           WHEN '01' THEN 'Amazonas'
           WHEN '02' THEN 'Áncash'
           WHEN '03' THEN 'Apurímac'
           WHEN '04' THEN 'Arequipa'
           WHEN '05' THEN 'Ayacucho'
           WHEN '06' THEN 'Cajamarca'
           WHEN '07' THEN 'Callao'
           WHEN '08' THEN 'Cusco'
           WHEN '09' THEN 'Huancavelica'
           WHEN '10' THEN 'Huánuco'
           WHEN '11' THEN 'Ica'
           WHEN '12' THEN 'Junín'
           WHEN '13' THEN 'La Libertad'
           WHEN '14' THEN 'Lambayeque'
           WHEN '15' THEN 'Lima'
           WHEN '16' THEN 'Loreto'
           WHEN '17' THEN 'Madre de Dios'
           WHEN '18' THEN 'Moquegua'
           WHEN '19' THEN 'Pasco'
           WHEN '20' THEN 'Piura'
           WHEN '21' THEN 'Puno'
           WHEN '22' THEN 'San Martín'
           WHEN '23' THEN 'Tacna'
           WHEN '24' THEN 'Tumbes'
           WHEN '25' THEN 'Ucayali'
           ELSE NVL(T.DPTO, 'Sin Ubicación')
       END                   AS CIUDAD,
       T.NUM_PEDIDOS,
       T.TOTAL_PEDIDO
FROM (
    SELECT SUBSTR(NVL(TRIM(C.COD_UBC), '00'), 1, 2) AS DPTO,
           COUNT(P.NUM_PED)              AS NUM_PEDIDOS,
           NVL(SUM(P.TOTAL_PEDIDO), 0)  AS TOTAL_PEDIDO
    FROM SIG.PEDIDO P
    LEFT JOIN CLIENTES C ON C.RUC = P.RUC
    WHERE P.ESTADO <> '9'
      AND P.FECHA >= :fechaInicio
      AND P.FECHA <  :fechaFin + 1
    GROUP BY SUBSTR(NVL(TRIM(C.COD_UBC), '00'), 1, 2)
) T
ORDER BY T.TOTAL_PEDIDO DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    result.Add(new DashZonaDto
                    {
                        Ciudad     = GetStr(reader, "CIUDAD"),
                        NumPedidos = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido = GetDec(reader, "TOTAL_PEDIDO")
                    });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en ObtenerPorZonaAsync"); }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Query 16 — Mix de Producto (Fibra / Color / Presentación)
        // ─────────────────────────────────────────────────────────
        public async Task<DashMixProductoResultDto> ObtenerMixProductoAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new DashMixProductoResultDto();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sqlFibra = @"
SELECT NVL(NULLIF(TRIM(I.TIPO_FIBRA), ''), 'Sin clasificar') AS CATEGORIA,
       NVL(SUM(I.CANTIDAD), 0)            AS CANT_PEDIDA,
       NVL(SUM(I.PRECIO * I.CANTIDAD), 0) AS VALOR_TOTAL
FROM SIG.ITEMPED I
INNER JOIN SIG.PEDIDO P ON P.NUM_PED = I.NUM_PED AND P.SERIE = I.SERIE
WHERE P.ESTADO <> '9'
  AND P.FECHA >= :fechaInicio AND P.FECHA < :fechaFin + 1
GROUP BY TRIM(I.TIPO_FIBRA)
ORDER BY CANT_PEDIDA DESC";

            const string sqlColor = @"
SELECT NVL(NULLIF(TRIM(I.COLOR_DET), ''), 'Sin clasificar') AS CATEGORIA,
       NVL(SUM(I.CANTIDAD), 0)            AS CANT_PEDIDA,
       NVL(SUM(I.PRECIO * I.CANTIDAD), 0) AS VALOR_TOTAL
FROM SIG.ITEMPED I
INNER JOIN SIG.PEDIDO P ON P.NUM_PED = I.NUM_PED AND P.SERIE = I.SERIE
WHERE P.ESTADO <> '9'
  AND P.FECHA >= :fechaInicio AND P.FECHA < :fechaFin + 1
GROUP BY TRIM(I.COLOR_DET)
ORDER BY CANT_PEDIDA DESC";

            const string sqlPres = @"
SELECT NVL(NULLIF(TRIM(I.PRESENTACION), ''), 'Sin clasificar') AS CATEGORIA,
       NVL(SUM(I.CANTIDAD), 0)            AS CANT_PEDIDA,
       NVL(SUM(I.PRECIO * I.CANTIDAD), 0) AS VALOR_TOTAL
FROM SIG.ITEMPED I
INNER JOIN SIG.PEDIDO P ON P.NUM_PED = I.NUM_PED AND P.SERIE = I.SERIE
WHERE P.ESTADO <> '9'
  AND P.FECHA >= :fechaInicio AND P.FECHA < :fechaFin + 1
GROUP BY TRIM(I.PRESENTACION)
ORDER BY CANT_PEDIDA DESC";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                foreach (var (sql, list) in new[] {
                    (sqlFibra, result.Fibra),
                    (sqlColor, result.Color),
                    (sqlPres,  result.Presentacion) })
                {
                    using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                    cmd.Parameters.Add("fechaInicio", OracleDbType.Date).Value = fechaInicio.Date;
                    cmd.Parameters.Add("fechaFin",    OracleDbType.Date).Value = fechaFin.Date;

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        list.Add(new DashMixDto
                        {
                            Categoria  = GetStr(reader, "CATEGORIA"),
                            CantPedida = GetDec(reader, "CANT_PEDIDA"),
                            ValorTotal = GetDec(reader, "VALOR_TOTAL")
                        });
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en ObtenerMixProductoAsync"); }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────
        private static string DescripcionEstado(string estado) => estado switch
        {
            "0" => "Emitido",
            "1" => "Emitido",
            "5" => "Aprobado",
            "6" => "Cerrado",
            "8" => "Anulado Pactado / Problema de Planta",
            "9" => "Anulado",
            _   => estado
        };
    }
}
