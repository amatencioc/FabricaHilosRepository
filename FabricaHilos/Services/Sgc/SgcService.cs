using Oracle.ManagedDataAccess.Client;
using System.Data;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    public interface ISgcService
    {
        Task<(List<PedidoSgcDto> Items, int TotalCount, decimal SumTotalPedido, decimal SumTotalDespacho)> ObtenerPedidosAsync(string? buscar, DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 10);
        Task<PedidoSgcDto?> ObtenerPedidoAsync(int serie, int numPed);
        Task<(List<ItemPedDto> Items, int TotalCount, decimal SumCantidad, decimal SumPrecio, decimal SumCantDespacho, decimal SumDifDespacho)> ObtenerDetallePedidoAsync(int serie, int numPed, int page = 1, int pageSize = 10);
        Task<(List<KardexGDto> Items, int TotalCount)> ObtenerGuiasAsync(int pedSerie, int numPed, int page = 1, int pageSize = 10);
        Task<KardexGDto?> ObtenerGuiaAsync(string codAlm, string tpTransac, int serie, int numero);
        Task<(List<KardexDDto> Items, int TotalCount)> ObtenerDetalleGuiaAsync(string codAlm, string tpTransac, int serie, int numero, int page = 1, int pageSize = 10);
        Task<(List<DocuVentDto> Items, int TotalCount)> ObtenerFacturasAsync(string? cTipo, string? cSerie, string? cNumero, int page = 1, int pageSize = 10);
        Task<DocuVentDto?> ObtenerFacturaAsync(string tipo, string serie, string numero);
        Task<(List<ItemDocuDto> Items, int TotalCount)> ObtenerDetalleFacturaAsync(string tipo, string serie, string numero, int page = 1, int pageSize = 10);
        Task<ItemPedDto?> ObtenerItemPedAsync(int numPed, int nro);
        Task<bool> TieneGuiasAsync(int pedSerie, int numPed);
        Task<(List<PackingGDto> Items, int TotalCount)> ObtenerPackingsAsync(int numPed, int page = 1, int pageSize = 10);
        Task<PackingGDto?> ObtenerPackingAsync(string tipo, int serie, int numero);
        Task<(List<DocuVentDto> Items, int TotalCount)> ObtenerFacturasPorPackingAsync(string tipo, int serie, int numero, int page = 1, int pageSize = 10);
        Task<SalidaInternaDto?> ObtenerSalidaInternaAsync(string codAlm, string tpTransac, int serie, int numero);
        Task<(List<DespachoListadoDto> Items, int TotalCount)> ObtenerListadoDespachosAsync(string? guia, string? pedido, string? factura, DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 10);
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

        public async Task<(List<PedidoSgcDto> Items, int TotalCount, decimal SumTotalPedido, decimal SumTotalDespacho)> ObtenerPedidosAsync(string? buscar, DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                _logger.LogWarning("Oracle connection string not configured");
                return ([], 0, 0m, 0m);
            }

            int startRow     = (page - 1) * pageSize + 1;
            int endRow       = page * pageSize;
            bool hasBuscar   = !string.IsNullOrWhiteSpace(buscar);
            bool hasFechaIni = fechaInicio.HasValue;
            bool hasFechaFin = fechaFin.HasValue;
            bool hasFechas   = hasFechaIni || hasFechaFin;

            string buscarFilter = hasBuscar ? @"
                      AND (UPPER(P.NOMBRE)    LIKE '%' || UPPER(:buscar) || '%'
                        OR UPPER(P.RUC)       LIKE '%' || UPPER(:buscar) || '%'
                        OR TO_CHAR(P.NUM_PED) LIKE :buscar || '%')" : string.Empty;

            string fechaClause;
            if (hasFechaIni && hasFechaFin)
                fechaClause = "\n                          AND TRUNC(G.FCH_TRANSAC) BETWEEN TRUNC(:fechaInicio) AND TRUNC(:fechaFin)";
            else if (hasFechaIni)
                fechaClause = "\n                          AND TRUNC(G.FCH_TRANSAC) = TRUNC(:fechaInicio)";
            else
                fechaClause = "\n                          AND TRUNC(G.FCH_TRANSAC) <= TRUNC(:fechaFin)";

            string fechaFilter = hasFechas ? $@"
                      AND EXISTS (
                          SELECT 1 FROM SIG.KARDEX_G G
                          WHERE TRIM(G.NRO_DOC_REF) = TO_CHAR(P.NUM_PED)
                            AND TRIM(G.SER_DOC_REF) = TO_CHAR(P.SERIE)
                            AND G.TIP_DOC_REF       = P.TIPO_DOCTO{fechaClause}
                      )" : string.Empty;

            // Los EXISTS van en la query EXTERNA: solo corren contra los 10 registros ya paginados
            string sql = $@"
                SELECT PAGED.TOTAL_COUNT, PAGED.SUM_TOTAL_PEDIDO,
                       PAGED.TOTAL_DESPACHO, PAGED.SUM_TOTAL_DESPACHO, PAGED.UNIDAD_DESPACHO,
                       PAGED.SERIE, PAGED.NUM_PED, PAGED.TIPO_DOCTO, PAGED.ESTADO, PAGED.FECHA,
                       PAGED.COD_CLIENTE, PAGED.NOMBRE, PAGED.RUC, PAGED.DETALLE,
                       PAGED.TOTAL_PEDIDO, PAGED.COD_VENDE, PAGED.MONEDA, PAGED.NRO_SUCUR,
                       CASE WHEN EXISTS (SELECT 1 FROM SIG.ITEMPED   I  WHERE I.NUM_PED  = PAGED.NUM_PED AND I.SERIE = PAGED.SERIE) THEN 1 ELSE 0 END AS CNT_DETALLE,
                       CASE WHEN EXISTS (SELECT 1 FROM SIG.PACKING_G PK WHERE PK.NUM_PED = PAGED.NUM_PED) THEN 1 ELSE 0 END AS CNT_PACKING
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY P.FECHA DESC, P.NUM_PED DESC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           SUM(NVL(P.TOTAL_PEDIDO, 0)) OVER() AS SUM_TOTAL_PEDIDO,
                           NVL(ID.TOTAL_DESPACHO, 0)              AS TOTAL_DESPACHO,
                           SUM(NVL(ID.TOTAL_DESPACHO, 0)) OVER()  AS SUM_TOTAL_DESPACHO,
                           ID.UNIDAD_DESPACHO,
                           P.SERIE, P.NUM_PED, P.TIPO_DOCTO, P.ESTADO, P.FECHA,
                           P.COD_CLIENTE, P.NOMBRE, P.RUC, P.DETALLE,
                           P.TOTAL_PEDIDO, P.COD_VENDE, P.MONEDA, P.NRO_SUCUR
                    FROM SIG.PEDIDO P
                    LEFT JOIN (
                        SELECT I.NUM_PED, I.SERIE,
                               SUM(NVL(I.CANTIDAD, 0) - NVL(CASE WHEN I.SALDO_R IS NOT NULL AND I.SALDO_R <> 0 THEN I.SALDO_R ELSE I.SALDO END, 0)) AS TOTAL_DESPACHO,
                               MIN(A.UNIDAD) AS UNIDAD_DESPACHO
                        FROM SIG.ITEMPED I
                        LEFT JOIN SIG.ARTICUL A ON A.COD_ART = I.COD_ART
                        GROUP BY I.NUM_PED, I.SERIE
                        ) ID ON ID.NUM_PED = P.NUM_PED AND ID.SERIE = P.SERIE
                        WHERE P.ESTADO <> '9'{buscarFilter}{fechaFilter}
                    ) PAGED
                WHERE PAGED.RN BETWEEN :startRow AND :endRow";

            var result = new List<PedidoSgcDto>();
            int totalCount = 0;
            decimal sumTotalPedido = 0m, sumTotalDespacho = 0m;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;

                if (hasBuscar)
                    cmd.Parameters.Add(new OracleParameter(":buscar",      OracleDbType.Varchar2, buscar,                  ParameterDirection.Input));
                if (hasFechaIni)
                    cmd.Parameters.Add(new OracleParameter(":fechaInicio", OracleDbType.Date,     fechaInicio!.Value.Date, ParameterDirection.Input));
                if (hasFechaFin)
                    cmd.Parameters.Add(new OracleParameter(":fechaFin",    OracleDbType.Date,     fechaFin!.Value.Date,    ParameterDirection.Input));

                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0)
                    {
                        totalCount        = GetInt(reader, "TOTAL_COUNT");
                        sumTotalPedido   = GetDec(reader, "SUM_TOTAL_PEDIDO")   ?? 0m;
                        sumTotalDespacho = GetDec(reader, "SUM_TOTAL_DESPACHO") ?? 0m;
                    }
                    result.Add(new PedidoSgcDto
                    {
                        Serie          = GetInt(reader, "SERIE"),
                        NumPed         = GetInt(reader, "NUM_PED"),
                        TipoDocto      = GetStr(reader, "TIPO_DOCTO"),
                        Estado         = GetStr(reader, "ESTADO"),
                        Fecha          = GetDt(reader, "FECHA"),
                        CodCliente     = GetStr(reader, "COD_CLIENTE"),
                        Nombre         = GetStr(reader, "NOMBRE"),
                        Ruc            = GetStr(reader, "RUC"),
                        Detalle        = GetStr(reader, "DETALLE"),
                        TotalPedido   = GetDec(reader, "TOTAL_PEDIDO"),
                        TotalDespacho = GetDec(reader, "TOTAL_DESPACHO"),
                        UnidadDespacho = GetStr(reader, "UNIDAD_DESPACHO"),
                        CodVende       = GetStr(reader, "COD_VENDE"),
                        Moneda         = GetStr(reader, "MONEDA"),
                        NroSucur       = GetStr(reader, "NRO_SUCUR"),
                        TieneDetalle   = GetInt(reader, "CNT_DETALLE") > 0,
                        TienePacking   = GetInt(reader, "CNT_PACKING") > 0
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pedidos SGC. Buscar: {Buscar}, FechaInicio: {FechaInicio}, FechaFin: {FechaFin}",
                    buscar, fechaInicio, fechaFin);
                throw;
            }

            return (result, totalCount, sumTotalPedido, sumTotalDespacho);
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

        public async Task<(List<ItemPedDto> Items, int TotalCount, decimal SumCantidad, decimal SumPrecio, decimal SumCantDespacho, decimal SumDifDespacho)> ObtenerDetallePedidoAsync(int serie, int numPed, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0, 0m, 0m, 0m, 0m);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            const string sql = @"
                SELECT RN, TOTAL_COUNT, SUM_CANTIDAD, SUM_PRECIO, SUM_CANT_DESPACHO, SUM_DIF_DESPACHO,
                       SERIE, NUM_PED, NRO, COD_ART, TIPO_FIBRA,
                       VALPF, CANTIDAD, PRECIO, SALDO, SALDO_R, IMP_VVB,
                       ESTADO, DETALLE, COLOR_DET, HILO_DET,
                       PRESENTACION, R_TIPO, R_SERIE, R_NUMERO,
                       A_DESCRIP, A_UNIDAD
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY I.NRO ASC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           SUM(NVL(I.CANTIDAD, 0)) OVER() AS SUM_CANTIDAD,
                           SUM(NVL(I.PRECIO, 0)) OVER() AS SUM_PRECIO,
                           SUM(NVL(I.CANTIDAD, 0) - NVL(CASE WHEN I.SALDO_R IS NOT NULL AND I.SALDO_R <> 0 THEN I.SALDO_R ELSE I.SALDO END, 0)) OVER() AS SUM_CANT_DESPACHO,
                           SUM(NVL(CASE WHEN I.SALDO_R IS NOT NULL AND I.SALDO_R <> 0 THEN I.SALDO_R ELSE I.SALDO END, 0)) OVER() AS SUM_DIF_DESPACHO,
                           I.SERIE, I.NUM_PED, I.NRO, I.COD_ART, I.TIPO_FIBRA,
                           I.VALPF, I.CANTIDAD, I.PRECIO, I.SALDO, I.SALDO_R, I.IMP_VVB,
                           I.ESTADO, I.DETALLE, I.COLOR_DET, I.HILO_DET,
                           I.PRESENTACION, I.R_TIPO, I.R_SERIE, I.R_NUMERO,
                           A.DESCRIPCION AS A_DESCRIP, A.UNIDAD AS A_UNIDAD
                     FROM SIG.ITEMPED I
                     LEFT JOIN SIG.ARTICUL A ON A.COD_ART = I.COD_ART
                     WHERE I.NUM_PED = :numPed
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var result = new List<ItemPedDto>();
            int totalCount = 0;
            decimal sumCantidad = 0m, sumPrecio = 0m, sumCantDespacho = 0m, sumDifDespacho = 0m;
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
                    if (result.Count == 0)
                    {
                        totalCount      = GetInt(reader, "TOTAL_COUNT");
                        sumCantidad     = GetDec(reader, "SUM_CANTIDAD")      ?? 0m;
                        sumPrecio       = GetDec(reader, "SUM_PRECIO")        ?? 0m;
                        sumCantDespacho = GetDec(reader, "SUM_CANT_DESPACHO") ?? 0m;
                        sumDifDespacho  = GetDec(reader, "SUM_DIF_DESPACHO")  ?? 0m;
                    }
                    result.Add(new ItemPedDto
                    {
                        Serie        = GetInt(reader, "SERIE"),
                        NumPed       = GetInt(reader, "NUM_PED"),
                        Nro          = GetInt(reader, "NRO"),
                        CodArt       = GetStr(reader, "COD_ART"),
                        TipoFibra    = GetStr(reader, "TIPO_FIBRA"),
                        Valpf        = GetStr(reader, "VALPF"),
                        Cantidad     = GetDec(reader, "CANTIDAD"),
                        Precio       = GetDec(reader, "PRECIO"),
                        Saldo        = GetDec(reader, "SALDO"),
                        SaldoR       = GetDec(reader, "SALDO_R"),
                        ImpVvb       = GetDec(reader, "IMP_VVB"),
                        Estado       = GetStr(reader, "ESTADO"),
                        Detalle      = GetStr(reader, "DETALLE"),
                        ColorDet     = GetStr(reader, "COLOR_DET"),
                        HiloDet      = GetStr(reader, "HILO_DET"),
                        Presentacion = GetStr(reader, "PRESENTACION"),
                        RTipo        = GetStr(reader, "R_TIPO"),
                        RSerie       = GetNullInt(reader, "R_SERIE"),
                        RNumero      = GetNullInt(reader, "R_NUMERO"),
                        Descripcion  = GetStr(reader, "A_DESCRIP"),
                        Unidad       = GetStr(reader, "A_UNIDAD")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle del pedido {Serie}/{NumPed}", serie, numPed);
                throw;
            }

            return (result, totalCount, sumCantidad, sumPrecio, sumCantDespacho, sumDifDespacho);
        }

        public async Task<ItemPedDto?> ObtenerItemPedAsync(int numPed, int nro)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return null;

            const string sql = @"
                SELECT I.SERIE, I.NUM_PED, I.NRO, I.COD_ART,
                       A.DESCRIPCION AS A_DESCRIP, A.UNIDAD AS A_UNIDAD
                FROM SIG.ITEMPED I
                LEFT JOIN SIG.ARTICUL A ON A.COD_ART = I.COD_ART
                WHERE I.NUM_PED = :numPed AND I.NRO = :nro";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":numPed", OracleDbType.Int32, numPed, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":nro",    OracleDbType.Int32, nro,    ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                if (await reader.ReadAsync())
                {
                    return new ItemPedDto
                    {
                        Serie       = GetInt(reader, "SERIE"),
                        NumPed      = GetInt(reader, "NUM_PED"),
                        Nro         = GetInt(reader, "NRO"),
                        CodArt      = GetStr(reader, "COD_ART"),
                        Descripcion = GetStr(reader, "A_DESCRIP"),
                        Unidad      = GetStr(reader, "A_UNIDAD")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ítem pedido {NumPed}/{Nro}", numPed, nro);
                throw;
            }

            return null;
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
                       NOMBRE, RUC, GLOSA, ESTADO, IND_FACT, PESO_TOTAL,
                       TIP_REF, SER_REF, NRO_REF, MOTIVO, MONEDA, SERIE_SUNAT, CNT_DETALLE
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY G.NUMERO ASC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           G.COD_ALM, G.TP_TRANSAC, G.SERIE, G.NUMERO, G.FCH_TRANSAC,
                           G.NOMBRE, G.RUC, G.GLOSA, G.ESTADO, G.IND_FACT, G.PESO_TOTAL,
                           G.TIP_REF, G.SER_REF, G.NRO_REF,
                           G.MOTIVO, G.MONEDA, G.SERIE_SUNAT,
                           (SELECT COUNT(*) FROM SIG.KARDEX_D D WHERE D.COD_ALM = G.COD_ALM AND D.TP_TRANSAC = G.TP_TRANSAC AND D.SERIE = G.SERIE AND D.NUMERO = G.NUMERO) AS CNT_DETALLE
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
                       G.NOMBRE, G.RUC, G.GLOSA, G.ESTADO, G.IND_FACT, G.PESO_TOTAL,
                       G.TIP_REF, G.SER_REF, G.NRO_REF,
                       G.MOTIVO, G.MONEDA, G.SERIE_SUNAT,
                       (SELECT COUNT(*) FROM SIG.KARDEX_D D WHERE D.COD_ALM = G.COD_ALM AND D.TP_TRANSAC = G.TP_TRANSAC AND D.SERIE = G.SERIE AND D.NUMERO = G.NUMERO) AS CNT_DETALLE
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
            Glosa      = GetStr(r, "GLOSA"),
            Estado     = GetStr(r, "ESTADO"),
            IndFact    = GetStr(r, "IND_FACT"),
            PesoTotal  = GetDec(r, "PESO_TOTAL"),
            TipRef     = GetStr(r, "TIP_REF"),
            SerRef     = GetStr(r, "SER_REF"),
            NroRef     = GetStr(r, "NRO_REF"),
            Motivo     = GetStr(r, "MOTIVO"),
            Moneda     = GetStr(r, "MONEDA"),
            SerieSunat = GetStr(r, "SERIE_SUNAT"),
            TieneDetalle = GetInt(r, "CNT_DETALLE") > 0
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
                       COD_ART, CANTIDAD, ESTADO, DETALLE, COLOR_DET, A_DESCRIP, A_UNIDAD
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY D.COD_ART ASC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           D.COD_ALM, D.TP_TRANSAC, D.SERIE, D.NUMERO,
                           D.COD_ART, D.CANTIDAD,
                           D.ESTADO, D.DETALLE, D.COLOR_DET,
                           A.DESCRIPCION AS A_DESCRIP,
                           A.UNIDAD      AS A_UNIDAD
                     FROM SIG.KARDEX_D D
                     INNER JOIN SIG.KARDEX_G G ON G.COD_ALM    = D.COD_ALM
                                              AND G.TP_TRANSAC  = D.TP_TRANSAC
                                              AND G.SERIE       = D.SERIE
                                              AND G.NUMERO      = D.NUMERO
                     LEFT JOIN SIG.ARTICUL A ON A.COD_ART = D.COD_ART
                     WHERE G.COD_ALM    = :codAlm
                       AND G.TP_TRANSAC = :tpTransac
                       AND G.SERIE      = :serie
                       AND G.NUMERO     = :numero
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
                        CodAlm      = GetStr(reader, "COD_ALM")    ?? string.Empty,
                        TpTransac   = GetStr(reader, "TP_TRANSAC") ?? string.Empty,
                        Serie       = GetInt(reader, "SERIE"),
                        Numero      = GetInt(reader, "NUMERO"),
                        Nro         = GetInt(reader, "RN"),
                        CodArt      = GetStr(reader, "COD_ART"),
                        Titulo      = null,
                        Cantidad    = cantidad,
                        Precio      = null,
                        Importe     = null,
                        Estado      = GetStr(reader, "ESTADO"),
                        Detalle     = GetStr(reader, "DETALLE"),
                        Descripcion = GetStr(reader, "A_DESCRIP"),
                        Unidad      = GetStr(reader, "A_UNIDAD"),
                        ColorDet    = GetStr(reader, "COLOR_DET")
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
                       NOMBRE, RUC, ESTADO, MONEDA, VAL_VENTA, IMP_IGV, PRECIO_VTA, CNT_DETALLE
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY F.FECHA DESC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           F.TIPODOC, F.SERIE, F.NUMERO, F.FECHA, F.COD_CLIENTE,
                           F.NOMBRE, F.RUC, F.ESTADO, F.MONEDA,
                           F.VAL_VENTA, F.IMP_IGV, F.PRECIO_VTA,
                           (SELECT COUNT(*) FROM SIG.ITEMDOCU D WHERE D.TIPODOC = F.TIPODOC AND TRIM(D.SERIE) = TRIM(F.SERIE) AND TRIM(D.NUMERO) = TRIM(F.NUMERO)) AS CNT_DETALLE
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
                       F.NOMBRE, F.RUC, F.ESTADO, F.MONEDA,
                       F.VAL_VENTA, F.IMP_IGV, F.PRECIO_VTA,
                       (SELECT COUNT(*) FROM SIG.ITEMDOCU D WHERE D.TIPODOC = F.TIPODOC AND TRIM(D.SERIE) = TRIM(F.SERIE) AND TRIM(D.NUMERO) = TRIM(F.NUMERO)) AS CNT_DETALLE
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
            Moneda     = GetStr(r, "MONEDA"),
            ValVenta   = GetDec(r, "VAL_VENTA"),
            ImpIgv     = GetDec(r, "IMP_IGV"),
            PrecioVta  = GetDec(r, "PRECIO_VTA"),
            TieneDetalle = GetInt(r, "CNT_DETALLE") > 0
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
                       COD_ART, CANTIDAD, VVTU, IMP_VVTA, DETALLE, A_DESCRIP
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY D.ORDEN ASC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           D.TIPODOC, D.SERIE, D.NUMERO,
                           D.COD_ART, D.CANTIDAD, D.VVTU, D.IMP_VVTA, D.DETALLE,
                           A.DESCRIPCION AS A_DESCRIP
                    FROM SIG.ITEMDOCU D
                    INNER JOIN SIG.DOCUVENT F ON F.TIPODOC = D.TIPODOC
                                             AND TRIM(F.SERIE)  = TRIM(D.SERIE)
                                             AND TRIM(F.NUMERO) = TRIM(D.NUMERO)
                    LEFT JOIN SIG.ARTICUL A ON A.COD_ART = D.COD_ART
                    WHERE F.TIPODOC = :tipo
                      AND TRIM(F.SERIE)  = TRIM(:serie)
                      AND TRIM(F.NUMERO) = TRIM(:numero)
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
                        Tipodoc     = GetStr(reader, "TIPODOC"),
                        Serie       = GetStr(reader, "SERIE"),
                        Numero      = GetStr(reader, "NUMERO"),
                        Nro         = GetInt(reader, "RN"),
                        CodArt      = GetStr(reader, "COD_ART"),
                        Titulo      = null,
                        Cantidad    = GetDec(reader, "CANTIDAD"),
                        Precio      = GetDec(reader, "VVTU"),
                        Importe     = GetDec(reader, "IMP_VVTA"),
                        Detalle     = GetStr(reader, "DETALLE"),
                        Descripcion = GetStr(reader, "A_DESCRIP")
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

        public async Task<bool> TieneGuiasAsync(int pedSerie, int numPed)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return false;

            const string sql = @"
                SELECT COUNT(*) FROM SIG.KARDEX_G G
                INNER JOIN SIG.PEDIDO P
                        ON TRIM(G.SER_DOC_REF) = TO_CHAR(P.SERIE)
                       AND G.TIP_DOC_REF        = P.TIPO_DOCTO
                       AND TRIM(G.NRO_DOC_REF)  = TO_CHAR(P.NUM_PED)
                WHERE P.SERIE   = :pedSerie
                  AND P.NUM_PED = :numPed";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":pedSerie", OracleDbType.Int32, pedSerie, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numPed",   OracleDbType.Int32, numPed,   ParameterDirection.Input));
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar guías para pedido {PedSerie}/{NumPed}", pedSerie, numPed);
                return false;
            }
        }

        // ========== PACKING_G ==========

        public async Task<(List<PackingGDto> Items, int TotalCount)> ObtenerPackingsAsync(int numPed, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            const string sql = @"
                SELECT RN, TOTAL_COUNT, TIPO, SERIE, NUMERO, OBSERVACION, SER_REF, NRO_REF, NUM_PED, NUM_ORDCOMPRA
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY P.SERIE, P.NUMERO) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           P.TIPO, P.SERIE, P.NUMERO, P.OBSERVACION, P.SER_REF, P.NRO_REF, P.NUM_PED, P.NUM_ORDCOMPRA
                    FROM SIG.PACKING_G P
                    WHERE P.NUM_PED = :pedido
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var result = new List<PackingGDto>();
            int totalCount = 0;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":pedido",   OracleDbType.Int32, numPed,   ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow",   OracleDbType.Int32, endRow,   ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0) totalCount = GetInt(reader, "TOTAL_COUNT");
                    result.Add(MapPackingG(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener packings para pedido {NumPed}", numPed);
                throw;
            }

            return (result, totalCount);
        }

        public async Task<PackingGDto?> ObtenerPackingAsync(string tipo, int serie, int numero)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return null;

            const string sql = @"
                SELECT P.TIPO, P.SERIE, P.NUMERO, P.OBSERVACION, P.SER_REF, P.NRO_REF, P.NUM_PED, P.NUM_ORDCOMPRA
                FROM SIG.PACKING_G P
                WHERE P.TIPO   = :tipo
                  AND P.SERIE  = :serie
                  AND P.NUMERO = :numero";

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":tipo",   OracleDbType.Varchar2, tipo,   ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":serie",  OracleDbType.Int32,    serie,  ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numero", OracleDbType.Int32,    numero, ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                if (await reader.ReadAsync())
                    return MapPackingG(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener packing {Tipo}/{Serie}/{Numero}", tipo, serie, numero);
                throw;
            }

            return null;
        }

        public async Task<(List<DocuVentDto> Items, int TotalCount)> ObtenerFacturasPorPackingAsync(string tipo, int serie, int numero, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            const string sql = @"
                SELECT RN, TOTAL_COUNT, TIPODOC, SERIE, NUMERO, FECHA, COD_CLIENTE,
                       NOMBRE, RUC, ESTADO, MONEDA, VAL_VENTA, IMP_IGV, PRECIO_VTA, CNT_DETALLE
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY F.FECHA DESC) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           F.TIPODOC, F.SERIE, F.NUMERO, F.FECHA, F.COD_CLIENTE,
                           F.NOMBRE, F.RUC, F.ESTADO, F.MONEDA,
                           F.VAL_VENTA, F.IMP_IGV, F.PRECIO_VTA,
                           (SELECT COUNT(*) FROM SIG.ITEMDOCU D WHERE D.TIPODOC = F.TIPODOC AND TRIM(D.SERIE) = TRIM(F.SERIE) AND TRIM(D.NUMERO) = TRIM(F.NUMERO)) AS CNT_DETALLE
                    FROM SIG.DOCUVENT F
                    WHERE F.TIP_DOC_REF = :tipo
                      AND TRIM(F.SER_DOC_REF) = TRIM(TO_CHAR(:serie))
                      AND TRIM(F.NRO_DOC_REF) = TRIM(TO_CHAR(:numero))
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
                cmd.Parameters.Add(new OracleParameter(":tipo",     OracleDbType.Varchar2, tipo,     ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":serie",    OracleDbType.Int32,    serie,    ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numero",   OracleDbType.Int32,    numero,   ParameterDirection.Input));
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
                _logger.LogError(ex, "Error al obtener facturas para packing {Tipo}/{Serie}/{Numero}", tipo, serie, numero);
                throw;
            }

            return (result, totalCount);
        }

        private static PackingGDto MapPackingG(OracleDataReader r) => new()
        {
            Tipo         = GetStr(r, "TIPO"),
            Serie        = GetInt(r, "SERIE"),
            Numero       = GetInt(r, "NUMERO"),
            Observacion  = GetStr(r, "OBSERVACION"),
            SerRef       = GetStr(r, "SER_REF"),
            NroRef       = GetStr(r, "NRO_REF"),
            NumPed       = GetInt(r, "NUM_PED"),
            NumOrdcompra = GetStr(r, "NUM_ORDCOMPRA")
        };

        // ========== SALIDA INTERNA (PDF para TP_TRANSAC = 23) ==========

        public async Task<SalidaInternaDto?> ObtenerSalidaInternaAsync(string codAlm, string tpTransac, int serie, int numero)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return null;

            const string sqlBase = @"
                SELECT G.COD_ALM, G.TP_TRANSAC, G.SERIE, G.NUMERO, G.FCH_TRANSAC,
                       G.NOMBRE, G.RUC, G.GLOSA, G.PESO_TOTAL,
                       G.TIP_REF, G.SER_REF, G.NRO_REF, G.NRO_DOC_REF,
                       G.MOTIVO
                FROM SIG.KARDEX_G G
                WHERE G.COD_ALM    = :codAlm
                  AND G.TP_TRANSAC = :tpTransac
                  AND G.SERIE      = :serie
                  AND G.NUMERO     = :numero";

            SalidaInternaDto? dto = null;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sqlBase, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter(":codAlm",    OracleDbType.Varchar2, codAlm,    ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":tpTransac", OracleDbType.Varchar2, tpTransac, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":serie",     OracleDbType.Int32,    serie,     ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":numero",    OracleDbType.Int32,    numero,    ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                if (await reader.ReadAsync())
                {
                    dto = new SalidaInternaDto
                    {
                        CodAlm     = GetStr(reader, "COD_ALM")    ?? string.Empty,
                        TpTransac  = GetStr(reader, "TP_TRANSAC") ?? string.Empty,
                        Serie      = GetInt(reader, "SERIE"),
                        Numero     = GetInt(reader, "NUMERO"),
                        FchTransac = GetDt(reader, "FCH_TRANSAC"),
                        Nombre     = GetStr(reader, "NOMBRE"),
                        Ruc        = GetStr(reader, "RUC"),
                        Glosa      = GetStr(reader, "GLOSA"),
                        PesoTotal  = GetDec(reader, "PESO_TOTAL"),
                        TipRef     = GetStr(reader, "TIP_REF"),
                        SerRef     = GetStr(reader, "SER_REF"),
                        NroRef     = GetStr(reader, "NRO_REF"),
                        NroDocRef  = GetStr(reader, "NRO_DOC_REF"),
                        Motivo     = GetStr(reader, "MOTIVO")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Salida Interna {CodAlm}/{TpTransac}/{Serie}/{Numero}", codAlm, tpTransac, serie, numero);
                throw;
            }

            if (dto == null) return null;

            // Campos extra (best-effort: si la columna no existe en el esquema se ignora)
            try
            {
                const string sqlExtra = @"
                    SELECT NOM_TRANSPOR, NRO_TRANSPOR, NOM_VEHICULO,
                           DIR_PARTIDA, DIR_LLEGADA, FCH_ENTREGA,
                           NRO_BULTOS, MOD_TRASLADO
                    FROM SIG.KARDEX_G
                    WHERE COD_ALM    = :codAlm
                      AND TP_TRANSAC = :tpTransac
                      AND SERIE      = :serie
                      AND NUMERO     = :numero";

                using var conn2 = new OracleConnection(connStr);
                await conn2.OpenAsync();
                using var cmd2 = new OracleCommand(sqlExtra, conn2);
                cmd2.BindByName = true;
                cmd2.Parameters.Add(new OracleParameter(":codAlm",    OracleDbType.Varchar2, codAlm,    ParameterDirection.Input));
                cmd2.Parameters.Add(new OracleParameter(":tpTransac", OracleDbType.Varchar2, tpTransac, ParameterDirection.Input));
                cmd2.Parameters.Add(new OracleParameter(":serie",     OracleDbType.Int32,    serie,     ParameterDirection.Input));
                cmd2.Parameters.Add(new OracleParameter(":numero",    OracleDbType.Int32,    numero,    ParameterDirection.Input));

                using var r2 = await cmd2.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                if (await r2.ReadAsync())
                {
                    dto.NomTranspor = GetStr(r2, "NOM_TRANSPOR");
                    dto.NroTranspor = GetStr(r2, "NRO_TRANSPOR");
                    dto.NomVehiculo = GetStr(r2, "NOM_VEHICULO");
                    dto.DirPartida  = GetStr(r2, "DIR_PARTIDA");
                    dto.DirLlegada  = GetStr(r2, "DIR_LLEGADA");
                    dto.FchEntrega  = GetDt(r2, "FCH_ENTREGA");
                    dto.NroBultos   = GetNullInt(r2, "NRO_BULTOS");
                    dto.ModTraslado = GetStr(r2, "MOD_TRASLADO");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Campos extra de KARDEX_G no disponibles para Salida Interna {Serie}/{Numero} — se generará el PDF con los datos base", serie, numero);
            }

            // Descripción del Motivo de Traslado desde TABLAS_AUXILIARES (TIPO=88, CODIGO=dto.Motivo)
            if (!string.IsNullOrWhiteSpace(dto.Motivo))
            {
                try
                {
                    const string sqlMotivo = @"
                        SELECT DESCRIPCION
                        FROM SIG.TABLAS_AUXILIARES
                        WHERE TIPO   = 88
                          AND CODIGO = :codigo";

                    using var connM = new OracleConnection(connStr);
                    await connM.OpenAsync();
                    using var cmdM = new OracleCommand(sqlMotivo, connM);
                    cmdM.BindByName = true;
                    cmdM.Parameters.Add(new OracleParameter(":codigo", OracleDbType.Varchar2, dto.Motivo, ParameterDirection.Input));

                    var desc = await cmdM.ExecuteScalarAsync();
                    if (desc != null && desc != DBNull.Value)
                        dto.Motivo = desc.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo obtener descripción de Motivo '{Motivo}' desde TABLAS_AUXILIARES", dto.Motivo);
                }
            }

            // Items de detalle (sin paginación)
            try
            {
                const string sqlItems = @"
                    SELECT D.COD_ART, A.DESCRIPCION, A.UNIDAD, D.CANTIDAD
                    FROM SIG.KARDEX_D D
                    LEFT JOIN SIG.ARTICUL A ON A.COD_ART = D.COD_ART
                    WHERE D.COD_ALM    = :codAlm
                      AND D.TP_TRANSAC = :tpTransac
                      AND D.SERIE      = :serie
                      AND D.NUMERO     = :numero
                    ORDER BY D.COD_ART";

                using var conn3 = new OracleConnection(connStr);
                await conn3.OpenAsync();
                using var cmd3 = new OracleCommand(sqlItems, conn3);
                cmd3.BindByName = true;
                cmd3.Parameters.Add(new OracleParameter(":codAlm",    OracleDbType.Varchar2, codAlm,    ParameterDirection.Input));
                cmd3.Parameters.Add(new OracleParameter(":tpTransac", OracleDbType.Varchar2, tpTransac, ParameterDirection.Input));
                cmd3.Parameters.Add(new OracleParameter(":serie",     OracleDbType.Int32,    serie,     ParameterDirection.Input));
                cmd3.Parameters.Add(new OracleParameter(":numero",    OracleDbType.Int32,    numero,    ParameterDirection.Input));

                using var r3 = await cmd3.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await r3.ReadAsync())
                {
                    dto.Items.Add(new SalidaInternaItemDto
                    {
                        CodArt      = GetStr(r3, "COD_ART"),
                        Descripcion = GetStr(r3, "DESCRIPCION"),
                        Unidad      = GetStr(r3, "UNIDAD"),
                        Cantidad    = GetDec(r3, "CANTIDAD")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener items Salida Interna {CodAlm}/{TpTransac}/{Serie}/{Numero}", codAlm, tpTransac, serie, numero);
            }

            return dto;
        }

        // ========== LISTADO DE DESPACHOS ==========

        public async Task<(List<DespachoListadoDto> Items, int TotalCount)> ObtenerListadoDespachosAsync(string? guia, string? pedido, string? factura, DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 10)
        {
            var connStr = GetOracleConnectionString();
            if (string.IsNullOrEmpty(connStr)) return ([], 0);

            int startRow = (page - 1) * pageSize + 1;
            int endRow   = page * pageSize;

            bool hasGuia      = !string.IsNullOrWhiteSpace(guia);
            bool hasPedido    = !string.IsNullOrWhiteSpace(pedido);
            bool hasFactura   = !string.IsNullOrWhiteSpace(factura);
            bool hasFechaIni  = fechaInicio.HasValue;
            bool hasFechaFin  = fechaFin.HasValue;

            string guiaFilter    = hasGuia    ? "\n                          AND G.NUMERO = :guia" : string.Empty;
            string pedidoFilter  = hasPedido  ? "\n                          AND TO_CHAR(P.NUM_PED) || '-' || TO_CHAR(I.NRO) = :pedido" : string.Empty;
            string facturaFilter = hasFactura ? "\n                          AND TRIM(F.NUMERO) LIKE '%' || TRIM(:factura) || '%'" : string.Empty;

            string fechaFilter = string.Empty;
            if (hasFechaIni && hasFechaFin)
                fechaFilter = "\n                          AND TRUNC(F.FECHA) BETWEEN TRUNC(:fechaInicio) AND TRUNC(:fechaFin)";
            else if (hasFechaIni)
                fechaFilter = "\n                          AND TRUNC(F.FECHA) >= TRUNC(:fechaInicio)";
            else if (hasFechaFin)
                fechaFilter = "\n                          AND TRUNC(F.FECHA) <= TRUNC(:fechaFin)";

            string sql = $@"
                SELECT RN, TOTAL_COUNT,
                       ""RAZON SOCIAL"", ""OC"", ""PEDIDO"", ""FACTURA"",
                       ""FECHA.DOC"", ""ARTICULO"", ""CANTIDAD"", ""PRECIO"",
                       ""GUIA"", ""OBS""
                FROM (
                    SELECT ROW_NUMBER() OVER (ORDER BY Q.""FECHA.DOC"" DESC NULLS LAST) AS RN,
                           COUNT(*) OVER() AS TOTAL_COUNT,
                           Q.""RAZON SOCIAL"",
                           Q.""OC"",
                           Q.""PEDIDO"",
                           Q.""FACTURA"",
                           Q.""FECHA.DOC"",
                           Q.""ARTICULO"",
                           Q.""CANTIDAD"",
                           Q.""PRECIO"",
                           Q.""GUIA"",
                           Q.""OBS""
                    FROM (
                        SELECT
                            P.NOMBRE                                                AS ""RAZON SOCIAL"",
                            MAX(PK.NUM_ORDCOMPRA)                                   AS ""OC"",
                            TO_CHAR(P.NUM_PED) || '-' || TO_CHAR(I.NRO)             AS ""PEDIDO"",
                            MAX(TRIM(F.NUMERO))                                     AS ""FACTURA"",
                            MAX(F.FECHA)                                            AS ""FECHA.DOC"",
                            MAX(A.DESCRIPCION)                                      AS ""ARTICULO"",
                            MAX(I.CANTIDAD)                                         AS ""CANTIDAD PEDIDO"",                            
                            MAX(I.PRECIO)                                           AS ""PRECIO"",
                            MAX(G.NUMERO)                                           AS ""GUIA"",
                            MAX(I.DETALLE)                                          AS ""OBS""
                        FROM SIG.ARTICUL A
                        INNER JOIN SIG.ITEMPED I
                                ON I.COD_ART = A.COD_ART
                        INNER JOIN SIG.PEDIDO P
                                ON P.NUM_PED = I.NUM_PED
                               AND P.SERIE   = I.SERIE
                        INNER JOIN SIG.KARDEX_G G
                                ON TRIM(G.NRO_DOC_REF) = TO_CHAR(P.NUM_PED)
                               AND TRIM(G.SER_DOC_REF) = TO_CHAR(P.SERIE)
                               AND G.TIP_DOC_REF       = P.TIPO_DOCTO
                        LEFT  JOIN SIG.DOCUVENT F
                                ON F.TIPODOC        = G.TIP_REF
                               AND TRIM(F.SERIE)    = TRIM(G.SER_REF)
                               AND TRIM(F.NUMERO)   = TRIM(G.NRO_REF)
                        LEFT  JOIN SIG.PACKING_G PK
                                ON PK.NUM_PED = P.NUM_PED
                        WHERE (INSTR(LOWER(A.FIBRA), 't') > 0 OR INSTR(A.FIBRA, '1') > 0)
                          AND P.ESTADO <> '9'{guiaFilter}{pedidoFilter}{facturaFilter}{fechaFilter}
                        GROUP BY
                            P.NOMBRE,
                            P.NUM_PED,
                            I.NRO
                    ) Q
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var result = new List<DespachoListadoDto>();
            int totalCount = 0;
            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;

                if (hasGuia)
                {
                    if (!int.TryParse(guia!.Trim(), out int guiaInt))
                        return (result, 0);
                    cmd.Parameters.Add(new OracleParameter(":guia", OracleDbType.Int32, guiaInt, ParameterDirection.Input));
                }
                if (hasPedido)
                    cmd.Parameters.Add(new OracleParameter(":pedido", OracleDbType.Varchar2, pedido!.Trim(), ParameterDirection.Input));
                if (hasFactura)
                    cmd.Parameters.Add(new OracleParameter(":factura", OracleDbType.Varchar2, factura!.Trim(), ParameterDirection.Input));
                if (hasFechaIni)
                    cmd.Parameters.Add(new OracleParameter(":fechaInicio", OracleDbType.Date, fechaInicio!.Value.Date, ParameterDirection.Input));
                if (hasFechaFin)
                    cmd.Parameters.Add(new OracleParameter(":fechaFin", OracleDbType.Date, fechaFin!.Value.Date, ParameterDirection.Input));

                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow", OracleDbType.Int32, endRow, ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (result.Count == 0)
                        totalCount = GetInt(reader, "TOTAL_COUNT");

                    result.Add(new DespachoListadoDto
                    {
                        Correlativo = GetInt(reader, "RN"),
                        RazonSocial = GetStr(reader, "RAZON SOCIAL"),
                        Oc          = GetStr(reader, "OC"),
                        Pedido      = GetStr(reader, "PEDIDO"),
                        Factura     = GetStr(reader, "FACTURA"),
                        FechaDoc    = GetDt(reader, "FECHA.DOC"),
                        Articulo    = GetStr(reader, "ARTICULO"),
                        Cantidad    = GetDec(reader, "CANTIDAD"),
                        Precio      = GetDec(reader, "PRECIO"),
                        Guia        = GetNullInt(reader, "GUIA"),
                        Obs         = GetStr(reader, "OBS")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener listado de despachos (guia={Guia}, pedido={Pedido}, factura={Factura}, fechaInicio={FechaInicio}, fechaFin={FechaFin})",
                    guia, pedido, factura, fechaInicio, fechaFin);
            }

            return (result, totalCount);
        }
    }
}
