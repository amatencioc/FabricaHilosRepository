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

    // P_DETALLE
    public async Task<List<IndLogisticaDetalleDto>> ObtenerDetalleAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        var result = new List<IndLogisticaDetalleDto>();
        await using var conn = new OracleConnection(GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_IND_LOGISTICA.P_DETALLE";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.Add("P_FECHA_DESDE", OracleDbType.Date).Value = fechaDesde;
        cmd.Parameters.Add("P_FECHA_HASTA", OracleDbType.Date).Value = fechaHasta;
        var cur = cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor);
        cur.Direction = ParameterDirection.Output;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new IndLogisticaDetalleDto
            {
                Tipo         = reader["TIPO"]          as string,
                NumReq       = Convert.ToInt64(reader["NUMREQ"]),
                Fecha        = ReadDate(reader, "FECHA"),
                FAutoriza    = ReadDate(reader, "F_AUTORIZA"),
                FRecibe      = ReadDate(reader, "F_RECIBE"),
                OrdenCompra  = reader["ORDEN_COMPRA"]  as string,
                FchOrden     = ReadDate(reader, "FCH_ORDEN"),
                Destino      = reader["DESTINO"]       as string,
                DescDestino  = reader["DESC_DESTINO"]  as string,
                Solicita     = reader["SOLICITA"]      as string,
                Observacion  = reader["OBSERVACION"]   as string,
                CodArt       = reader["COD_ART"]       as string,
                DescArticulo = reader["DESC_ARTICULO"] as string,
                Unidad       = reader["UNIDAD"]        as string,
                Cantidad     = ReadDec(reader, "CANTIDAD"),
                CantDesp     = ReadDec(reader, "CANT_DESP"),
                Saldo        = ReadDec(reader, "SALDO"),
                PUnit        = ReadDec(reader, "PUNIT"),
                SubTotal     = ReadDec(reader, "SUB_TOTAL"),
                Igv          = ReadDec(reader, "IGV"),
                Total        = ReadDec(reader, "TOTAL"),
                Estado       = reader["ESTADO"]        as string,
            });
        }
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
        cmd.Parameters.Add("P_FECHA_DESDE",    OracleDbType.Date).Value = fechaDesde;
        cmd.Parameters.Add("P_FECHA_HASTA",    OracleDbType.Date).Value = fechaHasta;

        var pResumen   = cmd.Parameters.Add("P_CUR_RESUMEN",    OracleDbType.RefCursor); pResumen.Direction   = ParameterDirection.Output;
        var pTiempos   = cmd.Parameters.Add("P_CUR_TIEMPOS",    OracleDbType.RefCursor); pTiempos.Direction   = ParameterDirection.Output;
        var pTopCcosto = cmd.Parameters.Add("P_CUR_TOP_CCOSTO", OracleDbType.RefCursor); pTopCcosto.Direction = ParameterDirection.Output;
        var pPend      = cmd.Parameters.Add("P_CUR_PENDIENTES", OracleDbType.RefCursor); pPend.Direction      = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync();

        await using (var r = ((OracleRefCursor)pResumen.Value).GetDataReader())
        {
            while (await r.ReadAsync())
            {
                vm.Resumen.Add(new IndLogisticaResumenDto
                {
                    Tipo        = r["TIPO"]         as string,
                    Estado      = r["ESTADO"]       as string,
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
                    Destino     = r["DESTINO"]      as string,
                    DescDestino = r["DESC_DESTINO"] as string,
                    TpDestino   = r["TP_DESTINO"]   as string,
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
                    NumReq         = Convert.ToInt64(r["NUMREQ"]),
                    Fecha          = ReadDate(r, "FECHA"),
                    Tipo           = r["TIPO"]          as string,
                    Estado         = r["ESTADO"]        as string,
                    Solicita       = r["SOLICITA"]      as string,
                    CodArt         = r["COD_ART"]       as string,
                    DescArticulo   = r["DESC_ARTICULO"] as string,
                    Saldo          = ReadDec(r, "SALDO"),
                    MontoPendiente = ReadDec(r, "MONTO_PENDIENTE"),
                    DiasEnEspera   = ReadInt(r, "DIAS_EN_ESPERA"),
                });
            }
        }

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
        cmd.Parameters.Add("P_FECHA_DESDE", OracleDbType.Date).Value = fechaDesde;
        cmd.Parameters.Add("P_FECHA_HASTA", OracleDbType.Date).Value = fechaHasta;
        var cur = cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor);
        cur.Direction = ParameterDirection.Output;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vm.Items.Add(new IndLogisticaCicloVidaDto
            {
                NumReq       = Convert.ToInt64(reader["NUMREQ"]),
                Tipo         = reader["TIPO"]         as string,
                NroOc        = reader["NRO_OC"]       as string,
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
        cmd.Parameters.Add("P_MESES_ATRAS", OracleDbType.Decimal).Value = mesesAtras;
        var cur = cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor);
        cur.Direction = ParameterDirection.Output;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vm.Items.Add(new IndLogisticaTendenciaMensualDto
            {
                Mes           = reader["MES"]           as string,
                CantReqs      = ReadInt(reader, "CANT_REQS"),
                T1Avg         = ReadDec(reader, "T1_AVG"),
                T2Avg         = ReadDec(reader, "T2_AVG"),
                T3Avg         = ReadDec(reader, "T3_AVG"),
                CicloAvg      = ReadDec(reader, "CICLO_AVG"),
                PctMismoDia   = ReadDec(reader, "PCT_MISMO_DIA"),
                PctHasta5Dias = ReadDec(reader, "PCT_HASTA_5DIAS"),
            });
        }
        return vm;
    }
}
