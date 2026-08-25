using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using FabricaHilos.Models.Logistica;

namespace FabricaHilos.Services.Logistica;

public interface IIndLogisticaService
{
    Task<List<IndLogisticaDetalleDto>>          ObtenerDetalleAsync(DateTime fechaDesde, DateTime fechaHasta);
    Task<IndLogisticaDashboardViewModel>        ObtenerDashboardAsync(DateTime fechaDesde, DateTime fechaHasta);
    Task<IndLogisticaCicloVidaViewModel>        ObtenerCicloVidaAsync(DateTime fechaDesde, DateTime fechaHasta);
    Task<IndLogisticaTendenciaMensualViewModel> ObtenerTendenciaMensualAsync(int mesesAtras = 12);
}

public class IndLogisticaService : IIndLogisticaService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IndLogisticaService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IndLogisticaService(
        IConfiguration configuration,
        ILogger<IndLogisticaService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _configuration       = configuration;
        _logger              = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetConnectionString()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var connKey = session?.GetString("EmpresaConexion") ?? "LaColonialConnection";
        return _configuration.GetConnectionString(connKey)
            ?? throw new InvalidOperationException($"Connection string '{connKey}' not found.");
    }

    private static DateTime? ReadDate(System.Data.Common.DbDataReader r, string col)
        => r[col] is DBNull ? null : Convert.ToDateTime(r[col]);

    private static decimal ReadDec(System.Data.Common.DbDataReader r, string col)
        => r[col] is DBNull ? 0m : Convert.ToDecimal(r[col]);

    private static int ReadInt(System.Data.Common.DbDataReader r, string col)
        => r[col] is DBNull ? 0 : Convert.ToInt32(r[col]);

    // Extrae string con Trim() para manejar columnas CHAR de Oracle (padding con espacios)
    private static string? ReadStr(System.Data.Common.DbDataReader r, string col)
        => r[col] is DBNull ? null : r[col].ToString()?.Trim();

    // Extrae un NUMBER de Oracle como long con guard de DBNull
    private static long ReadLong(System.Data.Common.DbDataReader r, string col)
        => r[col] is DBNull ? 0L : Convert.ToInt64(r[col]);

    // Extrae un NUMBER de Oracle como string entera (sin decimales).
    // Necesario porque Convert.ToString(OracleDecimal) devuelve "12345.00000000000000000000".
    private static string? ReadNumStr(System.Data.Common.DbDataReader r, string col)
        => r[col] is DBNull ? null : Convert.ToInt64(r[col]).ToString();

    // Registra un parámetro REF CURSOR de salida — evita código repetitivo en cada método
    private static OracleParameter AddOutCursor(OracleCommand cmd, string name)
    {
        var p = cmd.Parameters.Add(name, OracleDbType.RefCursor);
        p.Direction = ParameterDirection.Output;
        return p;
    }

    // P_DETALLE
    public async Task<List<IndLogisticaDetalleDto>> ObtenerDetalleAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        var result = new List<IndLogisticaDetalleDto>();
        await using var conn = new OracleConnection(GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_IND_LOGISTICA.P_DETALLE";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_FECHA_DESDE", OracleDbType.Date).Value = fechaDesde;
        cmd.Parameters.Add("P_FECHA_HASTA", OracleDbType.Date).Value = fechaHasta;
        AddOutCursor(cmd, "P_CURSOR");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new IndLogisticaDetalleDto
            {
                Tipo         = ReadStr(reader, "TIPO"),
                NumReq       = ReadLong(reader, "NUMREQ"),
                Fecha        = ReadDate(reader, "FECHA"),
                FAutoriza    = ReadDate(reader, "F_AUTORIZA"),
                FRecibe      = ReadDate(reader, "F_RECIBE"),
                OrdenCompra  = ReadNumStr(reader, "ORDEN_COMPRA"),
                FchOrden     = ReadDate(reader, "FCH_ORDEN"),
                Destino      = ReadStr(reader, "DESTINO"),
                DescDestino  = ReadStr(reader, "DESC_DESTINO"),
                Solicita     = ReadStr(reader, "SOLICITA"),
                Observacion  = ReadStr(reader, "OBSERVACION"),
                CodArt       = ReadStr(reader, "COD_ART"),
                DescArticulo = ReadStr(reader, "DESC_ARTICULO"),
                Unidad       = ReadStr(reader, "UNIDAD"),
                Cantidad     = ReadDec(reader, "CANTIDAD"),
                CantDesp     = ReadDec(reader, "CANT_DESP"),
                Saldo        = ReadDec(reader, "SALDO"),
                PUnit        = ReadDec(reader, "PUNIT"),
                SubTotal     = ReadDec(reader, "SUB_TOTAL"),
                Igv          = ReadDec(reader, "IGV"),
                Total        = ReadDec(reader, "TOTAL"),
                Moneda       = ReadStr(reader, "MONEDA"),
                TipoCambio   = ReadDec(reader, "TIPO_CAMBIO"),
                TotalSoles   = ReadDec(reader, "TOTAL_SOLES"),
                Estado       = ReadStr(reader, "ESTADO"),
            });
        }
        _logger.LogDebug("P_DETALLE: {Count} filas ({Desde} - {Hasta})", result.Count, fechaDesde, fechaHasta);
        return result;
    }

    // P_DASHBOARD
    public async Task<IndLogisticaDashboardViewModel> ObtenerDashboardAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        var vm = new IndLogisticaDashboardViewModel { FechaDesde = fechaDesde, FechaHasta = fechaHasta };

        await using var conn = new OracleConnection(GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_IND_LOGISTICA.P_DASHBOARD";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_FECHA_DESDE", OracleDbType.Date).Value = fechaDesde;
        cmd.Parameters.Add("P_FECHA_HASTA", OracleDbType.Date).Value = fechaHasta;
        var pResumen   = AddOutCursor(cmd, "P_CUR_RESUMEN");
        var pTiempos   = AddOutCursor(cmd, "P_CUR_TIEMPOS");
        var pTopCcosto = AddOutCursor(cmd, "P_CUR_TOP_CCOSTO");
        var pPend      = AddOutCursor(cmd, "P_CUR_PENDIENTES");

        await cmd.ExecuteNonQueryAsync();

        await using (var r = ((OracleRefCursor)pResumen.Value).GetDataReader())
        {
            while (await r.ReadAsync())
            {
                vm.Resumen.Add(new IndLogisticaResumenDto
                {
                    Tipo        = ReadStr(r, "TIPO"),
                    Estado      = ReadStr(r, "ESTADO"),
                    CantReqs    = ReadInt(r, "CANT_REQS"),
                    CantItems   = ReadInt(r, "CANT_ITEMS"),
                    MontoTotal  = ReadDec(r, "MONTO_TOTAL"),
                    PctAtendido = ReadDec(r, "PCT_ATENDIDO"),
                });
            }
        }

        await using (var r = ((OracleRefCursor)pTiempos.Value).GetDataReader())
        {
            if (await r.ReadAsync())
            {
                vm.Tiempos = new IndLogisticaTiemposDto
                {
                    TotalReqs           = ReadInt(r, "TOTAL_REQS"),
                    DiasRegAutorizacion = ReadDec(r, "DIAS_REG_AUTORIZACION"),
                    DiasAutRecibo       = ReadDec(r, "DIAS_AUT_RECIBO"),
                    DiasReciboOc        = ReadDec(r, "DIAS_RECIBO_OC"),
                    DiasCicloTotal      = ReadDec(r, "DIAS_CICLO_TOTAL"),
                };
            }
        }

        await using (var r = ((OracleRefCursor)pTopCcosto.Value).GetDataReader())
        {
            while (await r.ReadAsync())
            {
                vm.TopCcosto.Add(new IndLogisticaTopCcostoDto
                {
                    Destino     = ReadStr(r, "DESTINO"),
                    DescDestino = ReadStr(r, "DESC_DESTINO"),
                    TpDestino   = ReadStr(r, "TP_DESTINO"),
                    CantItems   = ReadInt(r, "CANT_ITEMS"),
                    CantReqs    = ReadInt(r, "CANT_REQS"),
                    MontoTotal  = ReadDec(r, "MONTO_TOTAL"),
                });
            }
        }

        await using (var r = ((OracleRefCursor)pPend.Value).GetDataReader())
        {
            while (await r.ReadAsync())
            {
                vm.Pendientes.Add(new IndLogisticaPendienteDto
                {
                    NumReq         = ReadLong(r, "NUMREQ"),
                    Fecha          = ReadDate(r, "FECHA"),
                    Tipo           = ReadStr(r, "TIPO"),
                    Estado         = ReadStr(r, "ESTADO"),
                    Solicita       = ReadStr(r, "SOLICITA"),
                    CodArt         = ReadStr(r, "COD_ART"),
                    DescArticulo   = ReadStr(r, "DESC_ARTICULO"),
                    Saldo          = ReadDec(r, "SALDO"),
                    MontoPendiente = ReadDec(r, "MONTO_PENDIENTE"),
                    DiasEnEspera   = ReadInt(r, "DIAS_EN_ESPERA"),
                });
            }
        }

        _logger.LogDebug("P_DASHBOARD: {Resumen} grupos, {Pend} pendientes ({Desde} - {Hasta})",
            vm.Resumen.Count, vm.Pendientes.Count, fechaDesde, fechaHasta);
        return vm;
    }

    // P_CICLO_VIDA
    public async Task<IndLogisticaCicloVidaViewModel> ObtenerCicloVidaAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        var vm = new IndLogisticaCicloVidaViewModel { FechaDesde = fechaDesde, FechaHasta = fechaHasta };

        await using var conn = new OracleConnection(GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_IND_LOGISTICA.P_CICLO_VIDA";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_FECHA_DESDE", OracleDbType.Date).Value = fechaDesde;
        cmd.Parameters.Add("P_FECHA_HASTA", OracleDbType.Date).Value = fechaHasta;
        AddOutCursor(cmd, "P_CURSOR");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vm.Items.Add(new IndLogisticaCicloVidaDto
            {
                NumReq       = ReadLong(reader, "NUMREQ"),
                Tipo         = ReadStr(reader, "TIPO"),
                NroOc        = ReadNumStr(reader, "NRO_OC"),
                FchRegistro  = ReadDate(reader, "FCH_REGISTRO"),
                FchAutoriza  = ReadDate(reader, "FCH_AUTORIZA"),
                FchReciboLog = ReadDate(reader, "FCH_RECIBO_LOG"),
                FchOc        = ReadDate(reader, "FCH_OC"),
                T1RegAut     = ReadInt(reader, "T1_REG_AUT"),
                T2AutRec     = ReadInt(reader, "T2_AUT_REC"),
                T3RecOc      = ReadInt(reader, "T3_REC_OC"),
                TCicloTotal  = ReadInt(reader, "T_CICLO_TOTAL"),
            });
        }
        _logger.LogDebug("P_CICLO_VIDA: {Count} reqs atendidas ({Desde} - {Hasta})", vm.Items.Count, fechaDesde, fechaHasta);
        return vm;
    }

    // P_TENDENCIA_MENSUAL
    public async Task<IndLogisticaTendenciaMensualViewModel> ObtenerTendenciaMensualAsync(int mesesAtras = 12)
    {
        var vm = new IndLogisticaTendenciaMensualViewModel { MesesAtras = mesesAtras };

        await using var conn = new OracleConnection(GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_IND_LOGISTICA.P_TENDENCIA_MENSUAL";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName  = true;
        cmd.Parameters.Add("P_MESES_ATRAS", OracleDbType.Int32).Value = mesesAtras;
        AddOutCursor(cmd, "P_CURSOR");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vm.Items.Add(new IndLogisticaTendenciaMensualDto
            {
                Mes           = ReadStr(reader, "MES"),
                CantReqs      = ReadInt(reader, "CANT_REQS"),
                T1Avg         = ReadDec(reader, "T1_AVG"),
                T2Avg         = ReadDec(reader, "T2_AVG"),
                T3Avg         = ReadDec(reader, "T3_AVG"),
                CicloAvg      = ReadDec(reader, "CICLO_AVG"),
                PctMismoDia   = ReadDec(reader, "PCT_MISMO_DIA"),
                PctHasta5Dias = ReadDec(reader, "PCT_HASTA_5DIAS"),
            });
        }
        _logger.LogDebug("P_TENDENCIA_MENSUAL: {Count} meses (ultimos {Meses})", vm.Items.Count, mesesAtras);
        return vm;
    }
}
