using Oracle.ManagedDataAccess.Client;
using System.Data;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class PedidoValorizadoEstService : OracleServiceBase, IPedidoValorizadoEstService
    {
        private readonly ILogger<PedidoValorizadoEstService> _logger;

        public PedidoValorizadoEstService(
            IConfiguration configuration,
            ILogger<PedidoValorizadoEstService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        // ── Helpers de lectura ─────────────────────────────────────────────────
        private static string? GetStr(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : r[col]?.ToString();

        private static DateTime? GetDate(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]);

        private static decimal GetDec(OracleDataReader r, string col)
        {
            try
            {
                if (r[col] == DBNull.Value) return 0m;
                var od = r.GetOracleDecimal(r.GetOrdinal(col));
                od = Oracle.ManagedDataAccess.Types.OracleDecimal.SetPrecision(od, 28);
                return (decimal)od;
            }
            catch { return 0m; }
        }

        private static int GetInt(OracleDataReader r, string col) => (int)GetDec(r, col);
        private static long GetLong(OracleDataReader r, string col) => (long)GetDec(r, col);

        // ── Listado principal (PKG_PED_VAL_EST.SP_LISTADO_PEDIDOS) ─────────────
        public async Task<List<PedidoValorizadoEstDto>> ListarAsync(PedidoValorizadoEstFiltroDto f)
        {
            var lista = new List<PedidoValorizadoEstDto>();
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return lista;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new OracleCommand($"{S}PKG_PED_VAL_EST.SP_LISTADO_PEDIDOS", conn)
                {
                    CommandType    = CommandType.StoredProcedure,
                    BindByName     = true,
                    CommandTimeout = 120
                };

                cmd.Parameters.Add("P_CLIENTE",      OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(f.Cliente) ? "%" : f.Cliente;
                cmd.Parameters.Add("P_EXCLUSION",    OracleDbType.Varchar2).Value = f.ExcluirAlmacen ? "EXCLUIR CLIENTE ALMACEN" : "SIN EXCLUSIONES";
                cmd.Parameters.Add("P_OPC_FPEDIDO",  OracleDbType.Varchar2).Value = f.OpcFPedido ?? "A LA FECHA";
                cmd.Parameters.Add("P_FECHAI",       OracleDbType.Date).Value     = (object?)f.FechaI?.Date ?? DBNull.Value;
                cmd.Parameters.Add("P_FECHAF",       OracleDbType.Date).Value     = (object?)f.FechaF?.Date ?? DBNull.Value;
                cmd.Parameters.Add("P_OPC_FENTREGA", OracleDbType.Varchar2).Value = f.OpcFEntrega ?? "TODOS";
                cmd.Parameters.Add("P_FECENT_INI",   OracleDbType.Date).Value     = (object?)f.FecEntIni?.Date ?? DBNull.Value;
                cmd.Parameters.Add("P_FECENT_FIN",   OracleDbType.Date).Value     = (object?)f.FecEntFin?.Date ?? DBNull.Value;
                cmd.Parameters.Add("P_OPCPAIS",      OracleDbType.Varchar2).Value = f.OpcPais ?? "TODOS";
                cmd.Parameters.Add("P_OPCV",         OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(f.Vendedor) ? "TODOS" : "POR VENDEDOR";
                cmd.Parameters.Add("P_VENDEDOR",     OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(f.Vendedor) ? "%" : f.Vendedor;
                cmd.Parameters.Add("P_ARTICULO",     OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(f.Articulo) ? "%" : f.Articulo;
                cmd.Parameters.Add("P_NUMPED",       OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(f.NumPed) ? "%" : f.NumPed;
                cmd.Parameters.Add("P_NRO",          OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(f.Nro) ? "%" : f.Nro;
                cmd.Parameters.Add("P_OCOMPRA",      OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(f.OCompra) ? "%" : f.OCompra;
                cmd.Parameters.Add("P_MATERIAL",     OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(f.Material) ? "%" : f.Material;
                cmd.Parameters.Add("P_CAMBIO",       OracleDbType.Decimal).Value  = f.Cambio <= 0 ? 1m : f.Cambio;
                cmd.Parameters.Add("P_CUR",          OracleDbType.RefCursor).Direction = ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new PedidoValorizadoEstDto
                    {
                        CodCliente        = GetStr(reader, "COD_CLIENTE"),
                        Nombre            = GetStr(reader, "NOMBRE"),
                        Fecha             = GetDate(reader, "FECHA"),
                        Entrega           = GetDate(reader, "ENTREGA"),
                        Nro               = GetInt(reader, "NRO"),
                        NumPed            = GetLong(reader, "NUM_PED"),
                        NumeroRef         = GetStr(reader, "NUMERO_REF"),
                        CodArt            = GetStr(reader, "COD_ART"),
                        Unidad            = GetStr(reader, "UNIDAD"),
                        Descripcion       = GetStr(reader, "DESCRIPCION"),
                        StockAct          = GetDec(reader, "STOCK_ACT"),
                        StockLote         = GetDec(reader, "STOCK_LOTE"),
                        Clieref           = GetStr(reader, "C_CLIEREF"),
                        Estado            = GetStr(reader, "ESTADO"),
                        TipoDocto         = GetStr(reader, "TIPO_DOCTO"),
                        Serie             = GetInt(reader, "SERIE"),
                        Moneda            = GetStr(reader, "MONEDA"),
                        Impsol            = GetDec(reader, "IMPSOL"),
                        Impdol            = GetDec(reader, "IMPDOL"),
                        Soles             = GetDec(reader, "SOLES"),
                        Cantidad          = GetDec(reader, "CANTIDAD"),
                        Dias              = GetInt(reader, "DIAS"),
                        Despachado        = GetDec(reader, "DESPACHADO"),
                        Saldo             = GetDec(reader, "SALDO"),
                        Detalle           = GetStr(reader, "DETALLE"),
                        FentregaMod       = GetDate(reader, "FENTREGA_MOD"),
                        FchEntregaMinmax  = GetStr(reader, "FCH_ENTREGA_MINMAX"),
                        EstatusDescripcion = GetStr(reader, "ESTATUS_COMBINED"),
                        CPago             = GetStr(reader, "C_PAGO"),
                        AnticipoSaldo     = GetDec(reader, "ANTICIPO_SALDO"),
                        IndSinAnticipo    = GetStr(reader, "IND_SIN_ANTICIPO") ?? "N"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PedValEst] Error al listar pedidos valorizados/estado");
            }

            return lista;
        }

        // ── Vendedores (TABLAS_AUXILIARES TIPO=29) ──────────────────────────────
        public async Task<List<VendedorDto>> ObtenerVendedoresAsync()
        {
            var lista = new List<VendedorDto>();
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return lista;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"SELECT CODIGO, DESCRIPCION FROM {S}TABLAS_AUXILIARES " +
                    "WHERE TIPO = 29 AND CODIGO NOT IN ('%', '....') ORDER BY DESCRIPCION", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new VendedorDto
                    {
                        CodVendedor = GetStr(reader, "CODIGO") ?? "",
                        Nombre      = GetStr(reader, "DESCRIPCION") ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PedValEst] No se pudo cargar catálogo de vendedores");
            }

            return lista;
        }

        // ── Búsqueda select2: clientes ───────────────────────────────────────────
        public async Task<List<Select2ItemDto>> BuscarClientesAsync(string term)
        {
            var lista = new List<Select2ItemDto>();
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr) || string.IsNullOrWhiteSpace(term)) return lista;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"SELECT * FROM (SELECT COD_CLIENTE, NOMBRE FROM {S}CLIENTES " +
                    "WHERE UPPER(COD_CLIENTE) LIKE :t OR UPPER(NOMBRE) LIKE :t ORDER BY NOMBRE) WHERE ROWNUM <= 20", conn)
                {
                    BindByName = true
                };
                cmd.Parameters.Add("t", OracleDbType.Varchar2).Value = "%" + term.Trim().ToUpperInvariant() + "%";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var cod    = GetStr(reader, "COD_CLIENTE") ?? "";
                    var nombre = GetStr(reader, "NOMBRE") ?? "";
                    lista.Add(new Select2ItemDto { Id = cod, Text = $"{cod} - {nombre}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PedValEst] Error al buscar clientes ({Term})", term);
            }

            return lista;
        }

        // ── Búsqueda select2: artículos ──────────────────────────────────────────
        public async Task<List<Select2ItemDto>> BuscarArticulosAsync(string term)
        {
            var lista = new List<Select2ItemDto>();
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr) || string.IsNullOrWhiteSpace(term)) return lista;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"SELECT * FROM (SELECT COD_ART, DESCRIPCION FROM {S}ARTICUL " +
                    "WHERE UPPER(COD_ART) LIKE :t OR UPPER(DESCRIPCION) LIKE :t ORDER BY COD_ART) WHERE ROWNUM <= 20", conn)
                {
                    BindByName = true
                };
                cmd.Parameters.Add("t", OracleDbType.Varchar2).Value = "%" + term.Trim().ToUpperInvariant() + "%";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var cod  = GetStr(reader, "COD_ART") ?? "";
                    var desc = GetStr(reader, "DESCRIPCION") ?? "";
                    lista.Add(new Select2ItemDto { Id = cod, Text = $"{cod} - {desc}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PedValEst] Error al buscar artículos ({Term})", term);
            }

            return lista;
        }
    }
}
