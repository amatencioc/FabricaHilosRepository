using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    public class DashboardSgcService : IDashboardSgcService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DashboardSgcService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DashboardSgcService(
            IConfiguration configuration,
            ILogger<DashboardSgcService> logger,
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
        // Query 1 — KPI global
        // ─────────────────────────────────────────────────────────
        public async Task<DashKpiDto> ObtenerKpiAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new DashKpiDto();
            if (string.IsNullOrEmpty(connStr)) return result;

            const string sql = @"
SELECT
    COUNT(*)                                                AS TOTAL_PEDIDOS,
    NVL(SUM(P.TOTAL_PEDIDO),    0)                          AS TOTAL_PEDIDO,
    NVL(SUM(P.TOTAL_FACTURADO), 0)                          AS TOTAL_FACTURADO,
    NVL(SUM(P.TOTAL_PEDIDO - NVL(P.TOTAL_FACTURADO, 0)), 0) AS TOTAL_PENDIENTE
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
                    result.TotalFacturado  = GetDec(reader, "TOTAL_FACTURADO");
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
WHERE P.ESTADO <> '9'
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
    COUNT(*)                                   AS NUM_PEDIDOS,
    NVL(SUM(P.TOTAL_PEDIDO),    0)             AS TOTAL_PEDIDO,
    NVL(SUM(P.TOTAL_FACTURADO), 0)             AS TOTAL_FACTURADO
FROM SIG.PEDIDO P
WHERE P.ESTADO <> '9'
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
                {
                    result.Add(new DashEvolucionDto
                    {
                        Mes            = GetStr(reader, "MES"),
                        NumPedidos     = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido    = GetDec(reader, "TOTAL_PEDIDO"),
                        TotalFacturado = GetDec(reader, "TOTAL_FACTURADO")
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
           COUNT(*)                    AS NUM_PEDIDOS,
           NVL(SUM(P.TOTAL_PEDIDO),    0) AS TOTAL_PEDIDO,
           NVL(SUM(P.TOTAL_FACTURADO), 0) AS TOTAL_FACTURADO,
           ROUND(
               NVL(SUM(P.TOTAL_FACTURADO), 0)
               / DECODE(NVL(SUM(P.TOTAL_PEDIDO), 0), 0, 1, NVL(SUM(P.TOTAL_PEDIDO), 0))
               * 100, 2
           ) AS PCT_FACTURADO
    FROM SIG.PEDIDO P
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
                        CodCliente     = GetStr(reader, "COD_CLIENTE"),
                        Nombre         = GetStr(reader, "NOMBRE"),
                        NumPedidos     = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido    = GetDec(reader, "TOTAL_PEDIDO"),
                        TotalFacturado = GetDec(reader, "TOTAL_FACTURADO"),
                        PctFacturado   = GetDec(reader, "PCT_FACTURADO")
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
SELECT
    P.COD_VENDE,
    NVL(TA.DESCRIPCION, P.COD_VENDE)  AS NOMBRE_VENDEDOR,
    COUNT(*)                            AS NUM_PEDIDOS,
    NVL(SUM(P.TOTAL_PEDIDO),    0)      AS TOTAL_PEDIDO,
    NVL(SUM(P.TOTAL_FACTURADO), 0)      AS TOTAL_FACTURADO,
    ROUND(
        NVL(SUM(P.TOTAL_FACTURADO), 0)
        / DECODE(NVL(SUM(P.TOTAL_PEDIDO), 0), 0, 1, NVL(SUM(P.TOTAL_PEDIDO), 0))
        * 100, 2
    ) AS PCT_FACTURADO
FROM SIG.PEDIDO P
LEFT JOIN SIG.TABLAS_AUXILIARES TA
       ON TA.TIPO   = 29
      AND TA.CODIGO = P.COD_VENDE
WHERE P.ESTADO <> '9'
  AND P.FECHA >= :fechaInicio
  AND P.FECHA <  :fechaFin + 1
GROUP BY P.COD_VENDE, TA.DESCRIPCION
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
                    result.Add(new DashVendedorDto
                    {
                        CodVende       = GetStr(reader, "COD_VENDE"),
                        NombreVendedor = GetStr(reader, "NOMBRE_VENDEDOR"),
                        NumPedidos     = GetInt(reader, "NUM_PEDIDOS"),
                        TotalPedido    = GetDec(reader, "TOTAL_PEDIDO"),
                        TotalFacturado = GetDec(reader, "TOTAL_FACTURADO"),
                        PctFacturado   = GetDec(reader, "PCT_FACTURADO")
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
        NVL(SUM(P.TOTAL_PEDIDO),    0) AS TOTAL_PEDIDO,
        NVL(SUM(P.TOTAL_FACTURADO), 0) AS TOTAL_FACTURADO
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
                        TotalPedido    = GetDec(reader, "TOTAL_PEDIDO"),
                        TotalFacturado = GetDec(reader, "TOTAL_FACTURADO")
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
        // Helpers
        // ─────────────────────────────────────────────────────────
        private static string DescripcionEstado(string estado) => estado switch
        {
            "0" => "Ingresado",
            "1" => "Aprobado",
            "2" => "En proceso",
            "3" => "Despachado",
            "4" => "Facturado",
            "5" => "Cerrado",
            "9" => "Anulado",
            _   => estado
        };
    }
}
