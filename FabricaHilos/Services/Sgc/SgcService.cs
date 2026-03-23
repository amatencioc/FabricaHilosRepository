using Oracle.ManagedDataAccess.Client;
using System.Data;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    public interface ISgcService
    {
        Task<(List<PedidoSgcDto> Items, int TotalCount)> ObtenerPedidosAsync(string? buscar, int page = 1, int pageSize = 10);
        Task<PedidoSgcDto?> ObtenerPedidoAsync(int serie, int numPed);
        Task<(List<ItemPedDto> Items, int TotalCount)> ObtenerDetallePedidoAsync(int serie, int numPed, int page = 1, int pageSize = 10);
        Task<(List<KardexGDto> Items, int TotalCount)> ObtenerGuiasAsync(int pedSerie, int numPed, int page = 1, int pageSize = 10);
        Task<KardexGDto?> ObtenerGuiaAsync(string codAlm, string tpTransac, int serie, int numero);
        Task<(List<KardexDDto> Items, int TotalCount)> ObtenerDetalleGuiaAsync(string codAlm, string tpTransac, int serie, int numero, int page = 1, int pageSize = 10);
        Task<(List<DocuVentDto> Items, int TotalCount)> ObtenerFacturasAsync(string? cTipo, string? cSerie, string? cNumero, int page = 1, int pageSize = 10);
        Task<DocuVentDto?> ObtenerFacturaAsync(string tipo, string serie, string numero);
        Task<(List<ItemDocuDto> Items, int TotalCount)> ObtenerDetalleFacturaAsync(string tipo, string serie, string numero, int page = 1, int pageSize = 10);
    }

    public class SgcService : ISgcService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SgcService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SgcService(IConfiguration configuration, ILogger<SgcService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetOracleConnectionString()
        {
            var oraUser = _httpContextAccessor.HttpContext?.Session.GetString("OracleUser");
            var oraPass = _httpContextAccessor.HttpContext?.Session.GetString("OraclePass");
            var baseConnStr = _configuration.GetConnectionString("OracleConnection") ?? string.Empty;

            if (!string.IsNullOrEmpty(oraUser) && !string.IsNullOrEmpty(oraPass))
            {
                var csBuilder = new OracleConnectionStringBuilder(baseConnStr)
                {
                    UserID = oraUser,
                    Password = oraPass
                };
                return csBuilder.ToString();
            }

            return baseConnStr;
        }

        private static string? GetStr(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : r[col]?.ToString();

        private static decimal? GetDec(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : Convert.ToDecimal(r[col]);

        private static DateTime? GetDt(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]);

        private static int GetInt(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);

        private static int? GetNullInt(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : Convert.ToInt32(r[col]);

        // ========== PEDIDO ==========

        public async Task<(List<PedidoSgcDto> Items, int TotalCount)> ObtenerPedidosAsync(string? buscar, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return ([], 0);
            }

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            var sql = new System.Text.StringBuilder(@"
                SELECT RN, TOTAL_COUNT, SERIE, NUM_PED, TIPO_DOCTO, ESTADO, FECHA,
                       COD_CLIENTE, NOMBRE, RUC, DETALLE,
                       TOTAL_PEDIDO, TOTAL_FACTURADO, COD_VENDE, MONEDA, NRO_SUCUR
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY P.FECHA DESC, P.NUM_PED DESC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           P.SERIE, P.NUM_PED, P.TIPO_DOCTO, P.ESTADO, P.FECHA,
                           P.COD_CLIENTE, P.NOMBRE, P.RUC, P.DETALLE,
                           P.TOTAL_PEDIDO, P.TOTAL_FACTURADO, P.COD_VENDE, P.MONEDA, P.NRO_SUCUR
                    FROM SIG.PEDIDO P
                    WHERE P.ESTADO <> '9'");

            if (!string.IsNullOrWhiteSpace(buscar))
                sql.Append(@"
                      AND (UPPER(P.NOMBRE)    LIKE '%' || UPPER(:buscar) || '%'
                        OR UPPER(P.RUC)       LIKE '%' || UPPER(:buscar) || '%'
                        OR TO_CHAR(P.NUM_PED) LIKE :buscar || '%')");

            sql.Append(@"
                )
                WHERE RN BETWEEN :startRow AND :endRow");

            var result = new List<PedidoSgcDto>();
            int totalCount = 0;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql.ToString(), conn);
                cmd.BindByName = true;

                if (!string.IsNullOrWhiteSpace(buscar))
                    cmd.Parameters.Add(new OracleParameter(":buscar",   OracleDbType.Varchar2, buscar,   ParameterDirection.Input));

                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0) totalCount = GetInt(reader, "TOTAL_COUNT");
                    result.Add(new PedidoSgcDto
                    {
                        Serie           = GetInt(reader, "SERIE"),
                        NumPed          = GetInt(reader, "NUM_PED"),
                        TipoDocto       = GetStr(reader, "TIPO_DOCTO"),
                        Estado          = GetStr(reader, "ESTADO"),
                        Fecha           = GetDt(reader, "FECHA"),
                        CodCliente      = GetStr(reader, "COD_CLIENTE"),
                        Nombre          = GetStr(reader, "NOMBRE"),
                        Ruc             = GetStr(reader, "RUC"),
                        Detalle         = GetStr(reader, "DETALLE"),
                        TotalPedido     = GetDec(reader, "TOTAL_PEDIDO"),
                        TotalFacturado  = GetDec(reader, "TOTAL_FACTURADO"),
                        CodVende        = GetStr(reader, "COD_VENDE"),
                        Moneda          = GetStr(reader, "MONEDA"),
                        NroSucur        = GetStr(reader, "NRO_SUCUR")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pedidos SGC. Buscar: {Buscar}", buscar);
                throw;
            }

            return (result, totalCount);
        }

        public async Task<PedidoSgcDto?> ObtenerPedidoAsync(int serie, int numPed)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return null;

            const string sql = @"
                SELECT P.SERIE, P.NUM_PED, P.TIPO_DOCTO, P.ESTADO, P.FECHA,
                       P.COD_CLIENTE, P.NOMBRE, P.RUC, P.DETALLE,
                       P.TOTAL_PEDIDO, P.TOTAL_FACTURADO, P.COD_VENDE, P.MONEDA, P.NRO_SUCUR
                FROM SIG.PEDIDO P
                WHERE P.SERIE = :serie AND P.NUM_PED = :numPed";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":serie",  OracleDbType.Int32, serie,  ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numPed", OracleDbType.Int32, numPed, ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                if (await reader.ReadAsync())
                {
                    return new PedidoSgcDto
                    {
                        Serie           = GetInt(reader, "SERIE"),
                        NumPed          = GetInt(reader, "NUM_PED"),
                        TipoDocto       = GetStr(reader, "TIPO_DOCTO"),
                        Estado          = GetStr(reader, "ESTADO"),
                        Fecha           = GetDt(reader, "FECHA"),
                        CodCliente      = GetStr(reader, "COD_CLIENTE"),
                        Nombre          = GetStr(reader, "NOMBRE"),
                        Ruc             = GetStr(reader, "RUC"),
                        Detalle         = GetStr(reader, "DETALLE"),
                        TotalPedido     = GetDec(reader, "TOTAL_PEDIDO"),
                        TotalFacturado  = GetDec(reader, "TOTAL_FACTURADO"),
                        CodVende        = GetStr(reader, "COD_VENDE"),
                        Moneda          = GetStr(reader, "MONEDA"),
                        NroSucur        = GetStr(reader, "NRO_SUCUR")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pedido SGC {Serie}/{NumPed}", serie, numPed);
                throw;
            }

            return null;
        }

        // ========== ITEMPED ==========

        public async Task<(List<ItemPedDto> Items, int TotalCount)> ObtenerDetallePedidoAsync(int serie, int numPed, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            const string sql = @"
                SELECT RN, TOTAL_COUNT, SERIE, NUM_PED, NRO, COD_ART, TITULO, TIPO_FIBRA,
                       VALPF, COLOR, CANTIDAD, PRECIO, SALDO, IMP_VVB,
                       ESTADO, DETALLE, COLOR_DET, HILO_DET, PROCESO,
                       PRESENTACION, R_TIPO, R_SERIE, R_NUMERO
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY I.NRO) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           I.SERIE, I.NUM_PED, I.NRO, I.COD_ART, I.TITULO, I.TIPO_FIBRA,
                           I.VALPF, I.COLOR, I.CANTIDAD, I.PRECIO, I.SALDO, I.IMP_VVB,
                           I.ESTADO, I.DETALLE, I.COLOR_DET, I.HILO_DET, I.PROCESO,
                           I.PRESENTACION, I.R_TIPO, I.R_SERIE, I.R_NUMERO
                    FROM SIG.ITEMPED I
                    WHERE I.NUM_PED = :numPed
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var result = new List<ItemPedDto>();
            int totalCount = 0;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":numPed",   OracleDbType.Int32, numPed,   ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0) totalCount = GetInt(reader, "TOTAL_COUNT");
                    result.Add(new ItemPedDto
                    {
                        Serie        = GetInt(reader, "SERIE"),
                        NumPed       = GetInt(reader, "NUM_PED"),
                        Nro          = GetInt(reader, "NRO"),
                        CodArt       = GetStr(reader, "COD_ART"),
                        Titulo       = GetStr(reader, "TITULO"),
                        TipoFibra    = GetStr(reader, "TIPO_FIBRA"),
                        Valpf        = GetStr(reader, "VALPF"),
                        Color        = GetStr(reader, "COLOR"),
                        Cantidad     = GetDec(reader, "CANTIDAD"),
                        Precio       = GetDec(reader, "PRECIO"),
                        Saldo        = GetDec(reader, "SALDO"),
                        ImpVvb       = GetDec(reader, "IMP_VVB"),
                        Estado       = GetStr(reader, "ESTADO"),
                        Detalle      = GetStr(reader, "DETALLE"),
                        ColorDet     = GetStr(reader, "COLOR_DET"),
                        HiloDet      = GetStr(reader, "HILO_DET"),
                        Proceso      = GetStr(reader, "PROCESO"),
                        Presentacion = GetStr(reader, "PRESENTACION"),
                        RTipo        = GetStr(reader, "R_TIPO"),
                        RSerie       = GetNullInt(reader, "R_SERIE"),
                        RNumero      = GetNullInt(reader, "R_NUMERO")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle del pedido {Serie}/{NumPed}", serie, numPed);
                throw;
            }

            return (result, totalCount);
        }

        // ========== KARDEX_G (Guías) ==========

        // Relación real confirmada:
        //   KARDEX_G.SER_DOC_REF = PEDIDO.SERIE
        //   KARDEX_G.TIP_DOC_REF = PEDIDO.TIPO_DOCTO
        //   KARDEX_G.NRO_DOC_REF = PEDIDO.NUM_PED
        public async Task<(List<KardexGDto> Items, int TotalCount)> ObtenerGuiasAsync(int pedSerie, int numPed, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            const string sql = @"
                SELECT RN, TOTAL_COUNT, COD_ALM, TP_TRANSAC, SERIE, NUMERO, FCH_TRANSAC,
                       NOMBRE, RUC, ESTADO, IND_FACT, PESO_TOTAL,
                       TIP_REF, SER_REF, NRO_REF, MOTIVO, MONEDA
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY G.FCH_TRANSAC DESC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           G.COD_ALM, G.TP_TRANSAC, G.SERIE, G.NUMERO, G.FCH_TRANSAC,
                           G.NOMBRE, G.RUC, G.ESTADO, G.IND_FACT, G.PESO_TOTAL,
                           G.TIP_REF, G.SER_REF, G.NRO_REF,
                           G.MOTIVO, G.MONEDA
                    FROM SIG.KARDEX_G G
                    INNER JOIN SIG.PEDIDO P
                            ON TRIM(G.SER_DOC_REF) = TO_CHAR(P.SERIE)
                           AND G.TIP_DOC_REF        = P.TIPO_DOCTO
                           AND TRIM(G.NRO_DOC_REF)  = TO_CHAR(P.NUM_PED)
                    WHERE P.SERIE   = :pedSerie
                      AND P.NUM_PED = :numPed
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var result = new List<KardexGDto>();
            int totalCount = 0;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":pedSerie", OracleDbType.Int32, pedSerie, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numPed",   OracleDbType.Int32, numPed,   ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0) totalCount = GetInt(reader, "TOTAL_COUNT");
                    result.Add(MapKardexG(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener guías para pedido {PedSerie}/{NumPed}", pedSerie, numPed);
                throw;
            }

            return (result, totalCount);
        }

        public async Task<KardexGDto?> ObtenerGuiaAsync(string codAlm, string tpTransac, int serie, int numero)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return null;

            const string sql = @"
                SELECT G.COD_ALM, G.TP_TRANSAC, G.SERIE, G.NUMERO, G.FCH_TRANSAC,
                       G.NOMBRE, G.RUC, G.ESTADO, G.IND_FACT, G.PESO_TOTAL,
                       G.TIP_REF, G.SER_REF, G.NRO_REF,
                       G.MOTIVO, G.MONEDA
                FROM SIG.KARDEX_G G
                WHERE G.COD_ALM   = :codAlm
                  AND G.TP_TRANSAC = :tpTransac
                  AND G.SERIE      = :serie
                  AND G.NUMERO     = :numero";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":codAlm",    OracleDbType.Varchar2, codAlm,    ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":tpTransac", OracleDbType.Varchar2, tpTransac, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":serie",     OracleDbType.Int32,    serie,     ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numero",    OracleDbType.Int32,    numero,    ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                if (await reader.ReadAsync())
                    return MapKardexG(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener guía {CodAlm}/{TpTransac}/{Serie}/{Numero}", codAlm, tpTransac, serie, numero);
                throw;
            }

            return null;
        }

        private static KardexGDto MapKardexG(OracleDataReader r) => new()
        {
            CodAlm     = GetStr(r, "COD_ALM")    ?? string.Empty,
            TpTransac  = GetStr(r, "TP_TRANSAC") ?? string.Empty,
            Serie      = GetInt(r, "SERIE"),
            Numero     = GetInt(r, "NUMERO"),
            FchTransac = GetDt(r, "FCH_TRANSAC"),
            Nombre     = GetStr(r, "NOMBRE"),
            Ruc        = GetStr(r, "RUC"),
            Glosa      = null,
            Estado     = GetStr(r, "ESTADO"),
            IndFact    = GetStr(r, "IND_FACT"),
            PesoTotal  = GetDec(r, "PESO_TOTAL"),
            TipRef     = GetStr(r, "TIP_REF"),
            SerRef     = GetStr(r, "SER_REF"),
            NroRef     = GetStr(r, "NRO_REF"),
            Motivo     = GetStr(r, "MOTIVO"),
            Moneda     = GetStr(r, "MONEDA")
        };

        // ========== KARDEX_D (Detalle de Guía) ==========

        // NOTE: Ajuste los nombres de columna de SIG.KARDEX_D según su esquema real.
        public async Task<(List<KardexDDto> Items, int TotalCount)> ObtenerDetalleGuiaAsync(string codAlm, string tpTransac, int serie, int numero, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            const string sql = @"
                SELECT RN, TOTAL_COUNT, COD_ALM, TP_TRANSAC, SERIE, NUMERO,
                       COD_ART, CANTIDAD, ESTADO, DETALLE
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY D.COD_ART) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           D.COD_ALM, D.TP_TRANSAC, D.SERIE, D.NUMERO,
                           D.COD_ART, D.CANTIDAD,
                           D.ESTADO, D.DETALLE
                    FROM SIG.KARDEX_D D
                    WHERE D.COD_ALM    = :codAlm
                      AND D.TP_TRANSAC = :tpTransac
                      AND D.SERIE      = :serie
                      AND D.NUMERO     = :numero
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var result = new List<KardexDDto>();
            int totalCount = 0;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":codAlm",    OracleDbType.Varchar2, codAlm,    ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":tpTransac", OracleDbType.Varchar2, tpTransac, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":serie",     OracleDbType.Int32,    serie,     ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numero",    OracleDbType.Int32,    numero,    ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":startRow",  OracleDbType.Int32,    startRow,  ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow",    OracleDbType.Int32,    endRow,    ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0) totalCount = GetInt(reader, "TOTAL_COUNT");
                    var cantidad = GetDec(reader, "CANTIDAD");
                    result.Add(new KardexDDto
                    {
                        CodAlm    = GetStr(reader, "COD_ALM")    ?? string.Empty,
                        TpTransac = GetStr(reader, "TP_TRANSAC") ?? string.Empty,
                        Serie     = GetInt(reader, "SERIE"),
                        Numero    = GetInt(reader, "NUMERO"),
                        Nro       = GetInt(reader, "RN"),
                        CodArt    = GetStr(reader, "COD_ART"),
                        Titulo    = null,
                        Cantidad  = cantidad,
                        Precio    = null,
                        Importe   = null,
                        Estado    = GetStr(reader, "ESTADO"),
                        Detalle   = GetStr(reader, "DETALLE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de guía {CodAlm}/{TpTransac}/{Serie}/{Numero}", codAlm, tpTransac, serie, numero);
                throw;
            }

            return (result, totalCount);
        }

        // ========== DOCUVENT (Facturas) ==========

        // NOTE: Los campos C_TIPO, C_SERIE, C_NUMERO de KARDEX_G referencian a DOCUVENT.
        //       Ajuste los nombres de columna de SIG.DOCUVENT según su esquema real.
        public async Task<(List<DocuVentDto> Items, int TotalCount)> ObtenerFacturasAsync(string? cTipo, string? cSerie, string? cNumero, int page = 1, int pageSize = 10)
        {
            if (string.IsNullOrEmpty(cTipo) || string.IsNullOrEmpty(cSerie) || string.IsNullOrEmpty(cNumero))
                return ([], 0);

            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            const string sql = @"
                SELECT RN, TOTAL_COUNT, TIPODOC, SERIE, NUMERO, FECHA, COD_CLIENTE,
                       NOMBRE, RUC, ESTADO, MONEDA
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY F.FECHA DESC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           F.TIPODOC, F.SERIE, F.NUMERO, F.FECHA, F.COD_CLIENTE,
                           F.NOMBRE, F.RUC, F.ESTADO, F.MONEDA
                    FROM SIG.DOCUVENT F
                    WHERE F.TIPODOC = :tipo
                      AND TRIM(F.SERIE)  = TRIM(:serie)
                      AND TRIM(F.NUMERO) = TRIM(:numero)
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var result = new List<DocuVentDto>();
            int totalCount = 0;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":tipo",     OracleDbType.Varchar2, cTipo,    ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":serie",    OracleDbType.Varchar2, cSerie,   ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numero",   OracleDbType.Varchar2, cNumero,  ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32,    startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32,    endRow,   ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0) totalCount = GetInt(reader, "TOTAL_COUNT");
                    result.Add(MapDocuVent(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener facturas {CTipo}/{CSerie}/{CNumero}", cTipo, cSerie, cNumero);
                throw;
            }

            return (result, totalCount);
        }

        public async Task<DocuVentDto?> ObtenerFacturaAsync(string tipo, string serie, string numero)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return null;

            const string sql = @"
                SELECT F.TIPODOC, F.SERIE, F.NUMERO, F.FECHA, F.COD_CLIENTE,
                       F.NOMBRE, F.RUC, F.ESTADO, F.MONEDA
                FROM SIG.DOCUVENT F
                WHERE F.TIPODOC = :tipo
                  AND TRIM(F.SERIE)  = TRIM(:serie)
                  AND TRIM(F.NUMERO) = TRIM(:numero)";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":tipo",   OracleDbType.Varchar2, tipo,   ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":serie",  OracleDbType.Varchar2, serie,  ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numero", OracleDbType.Varchar2, numero, ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                if (await reader.ReadAsync())
                    return MapDocuVent(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener factura {Tipo}/{Serie}/{Numero}", tipo, serie, numero);
                throw;
            }

            return null;
        }

        private static DocuVentDto MapDocuVent(OracleDataReader r) => new()
        {
            Tipodoc    = GetStr(r, "TIPODOC"),
            Serie      = GetStr(r, "SERIE"),
            Numero     = GetStr(r, "NUMERO"),
            Fecha      = GetDt(r, "FECHA"),
            CodCliente = GetStr(r, "COD_CLIENTE"),
            Nombre     = GetStr(r, "NOMBRE"),
            Ruc        = GetStr(r, "RUC"),
            Total      = null,
            Estado     = GetStr(r, "ESTADO"),
            Glosa      = null,
            Moneda     = GetStr(r, "MONEDA")
        };

        // ========== ITEMDOCU (Detalle de Factura) ==========

        // NOTE: Ajuste los nombres de columna de SIG.ITEMDOCU según su esquema real.
        public async Task<(List<ItemDocuDto> Items, int TotalCount)> ObtenerDetalleFacturaAsync(string tipo, string serie, string numero, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            const string sql = @"
                SELECT RN, TOTAL_COUNT, TIPODOC, SERIE, NUMERO,
                       COD_ART, CANTIDAD, PVTU, IMP_VVTA, DETALLE
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY D.COD_ART) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           D.TIPODOC, D.SERIE, D.NUMERO,
                           D.COD_ART, D.CANTIDAD, D.PVTU, D.IMP_VVTA, D.DETALLE
                    FROM SIG.ITEMDOCU D
                    WHERE D.TIPODOC = :tipo
                      AND TRIM(D.SERIE)  = TRIM(:serie)
                      AND TRIM(D.NUMERO) = TRIM(:numero)
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var result = new List<ItemDocuDto>();
            int totalCount = 0;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":tipo",     OracleDbType.Varchar2, tipo,     ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":serie",    OracleDbType.Varchar2, serie,    ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numero",   OracleDbType.Varchar2, numero,   ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32,    startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32,    endRow,   ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0) totalCount = GetInt(reader, "TOTAL_COUNT");
                    result.Add(new ItemDocuDto
                    {
                        Tipodoc  = GetStr(reader, "TIPODOC"),
                        Serie    = GetStr(reader, "SERIE"),
                        Numero   = GetStr(reader, "NUMERO"),
                        Nro      = GetInt(reader, "RN"),
                        CodArt   = GetStr(reader, "COD_ART"),
                        Titulo   = null,
                        Cantidad = GetDec(reader, "CANTIDAD"),
                        Precio   = GetDec(reader, "PVTU"),
                        Importe  = GetDec(reader, "IMP_VVTA"),
                        Detalle  = GetStr(reader, "DETALLE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de factura {Tipo}/{Serie}/{Numero}", tipo, serie, numero);
                throw;
            }

            return (result, totalCount);
        }
    }
}
