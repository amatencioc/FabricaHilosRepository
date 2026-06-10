using System.Data;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnReporteService : OracleServiceBase, IPlnReporteService
{
    // Tamaño del buffer de fetch Oracle para el SP principal (~1 MB reduce round-trips)
    private const long FetchSizeBytes = 1 * 1024 * 1024;

    // Timeout para el SP principal (reporte complejo con muchos JOINs)
    private const int ReportTimeoutSeconds = 120;

    // Timeout para los SPs de combos (consultas ligeras)
    private const int ComboTimeoutSeconds = 30;

    // TTL de caché para los combos (semi-estáticos; se invalidan al cumplir los 10 min)
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;

    public PlnReporteService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache         cache)
        : base(configuration, httpContextAccessor)
    {
        _cache = cache;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static DateTime? D(object? v) =>
        v == null || v == DBNull.Value ? null : Convert.ToDateTime(v);

    private static string? Str(object? v) =>
        v == null || v == DBNull.Value ? null : v.ToString()?.Trim();

    private static decimal? Dec(object? v) =>
        v == null || v == DBNull.Value ? null : Convert.ToDecimal(v);

    // ── SP_PLN_SEG_PROG_TINTORERIA ────────────────────────────────────────────
    public async Task<IEnumerable<PlnReporteProduccion>> GetReporteProduccionAsync(
        string            opc,
        DateTime?         fechaIni          = null,
        DateTime?         fechaFin          = null,
        long?             numPed            = null,
        string            cliente           = "%",
        string            asesor            = "%",
        string            titulo            = "%",
        string            fibra             = "%",
        string            proceso           = "%",
        CancellationToken ct                = default)
    {
        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_SEG_PROG_TINTORERIA";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = ReportTimeoutSeconds;

        cmd.Parameters.Add("p_opc",     OracleDbType.Varchar2).Value = opc;
        var pFechaI = cmd.Parameters.Add("p_fechai", OracleDbType.Date);
        pFechaI.Value = fechaIni.HasValue ? (object)fechaIni.Value : DBNull.Value;
        var pFechaF = cmd.Parameters.Add("p_fechaf", OracleDbType.Date);
        pFechaF.Value = fechaFin.HasValue ? (object)fechaFin.Value : DBNull.Value;
        var pNumPed = cmd.Parameters.Add("p_numped", OracleDbType.Int64);
        pNumPed.Value = numPed.HasValue ? (object)numPed.Value : DBNull.Value;
        cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = cliente;
        cmd.Parameters.Add("p_asesor",  OracleDbType.Varchar2).Value = asesor;
        cmd.Parameters.Add("p_titulo",  OracleDbType.Varchar2).Value = titulo;
        cmd.Parameters.Add("p_fibra",   OracleDbType.Varchar2).Value = fibra;
        cmd.Parameters.Add("p_proceso", OracleDbType.Varchar2).Value = proceso;

        var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
        pCursor.Direction = ParameterDirection.Output;

        var list = new List<PlnReporteProduccion>();
        await using var r = (OracleDataReader)await cmd.ExecuteReaderAsync(ct);

        // Ampliar el buffer de fetch para reducir round-trips a Oracle
        r.FetchSize = cmd.RowSize > 0
            ? (long)Math.Ceiling((double)FetchSizeBytes / cmd.RowSize) * cmd.RowSize
            : FetchSizeBytes;

        while (await r.ReadAsync(ct))
        {
            list.Add(new PlnReporteProduccion
            {
                Partida             = Str(r["PARTIDA"]),
                EstadoProg          = Str(r["ESTADO_PROG"]),
                Cliente             = Str(r["CLIENTE"]),
                Material            = Str(r["MATERIAL"]),
                FchPedido           = D(r["FCH_PEDIDO"]),
                FchEntrega          = D(r["FHC_ENTREGA"]),
                FchPartida          = Str(r["FCH_PARTIDA"]),
                PesoNeto            = Dec(r["PESO_NETO"]),
                Rmc                 = Str(r["RMC"]),
                NroRmc              = Str(r["NRO_RMC"]),
                Referencia          = Str(r["REFERENCIA"]),
                Proceso             = Str(r["PROCESO"]),
                FechaTenido         = D(r["FECHA_TENIDO"]),
                FechaCcalid         = D(r["FECHA_CCALID"]),
                FechaEncon          = D(r["FECHA_ENCON"]),
                FechaSecado         = D(r["FECHA_SECADO"]),
                FechaReceta         = D(r["FECHA_RECETA"]),
                FchRevisado         = D(r["FCH_REVISADO"]),
                FechaIng            = D(r["FECHA_ING"]),
                CantDesp            = Dec(r["CANT_DESP"]),
                Titulo              = Str(r["TITULO"]),
                CantProg            = Dec(r["CANT_PROG"]),
                Lote                = Str(r["LOTE"]),
                TituloTexto         = Str(r["TITULO_TEXTO"]),
                FchProg             = D(r["FCH_PROG"]),
                PartMatiz           = Str(r["PART_MATIZ"]),
                EstEvaluacion       = Str(r["EST_EVALUACION"]),
                Defecto             = Str(r["DEFECTO"]),
                Resultado           = Str(r["RESULTADO"]),
                DiasRetraso         = Dec(r["DIAS_RETRASO"]),
                FchEntregaConoUno   = D(r["FCH_ENTREGA_CONO_UNO"]),
                FchValRec           = D(r["FCH_VAL_REC"]),
                FchEstimaConoUno    = D(r["FCH_ESTIMA_CONO_UNO"]),
                FchEntTin           = D(r["FCH_ENT_TIN"]),
                FchEstimaTenido     = D(r["FCH_ESTIMA_TENIDO"]),
                FchProgval          = D(r["FCH_PROGVAL"]),
                LaboVal             = Str(r["LABO_VAL"]),
                FchUltIngAlmpi      = D(r["FCH_ULT_ING_ALMPI"]),
                MaqProg             = Str(r["MAQ_PROG"]),
                AcaMad              = Str(r["ACA_MAD"]),
                FechaSecadoMad      = D(r["FECHA_SECADO_MAD"]),
            });
        }
        return list;
    }

    // ── Helper caché de combos ────────────────────────────────────────────────
    // Clave incluye el esquema (S) para soportar multi-empresa sin mezclar datos.
    private string CacheKey(string combo) => $"PlnFiltro:{S}{combo}";

    private async Task<List<T>> GetCachedComboAsync<T>(
        string   cacheKey,
        string   spName,
        Func<System.Data.Common.DbDataReader, T> map)
    {
        if (_cache.TryGetValue(cacheKey, out List<T>? cached) && cached is not null)
            return cached;

        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText    = $"{S}{spName}";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = ComboTimeoutSeconds;
        var p = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
        p.Direction = ParameterDirection.Output;

        var list = new List<T>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(map(r));

        _cache.Set(cacheKey, list, CacheTtl);
        return list;
    }

    // ── SP_PLN_FILTRO_CLIENTES ────────────────────────────────────────────────
    public Task<IEnumerable<PlnFiltroCliente>> GetFiltroClientesAsync() =>
        GetCachedComboAsync(
            CacheKey("CLIENTES"),
            "PKG_PLN.SP_PLN_FILTRO_CLIENTES",
            r => new PlnFiltroCliente
            {
                CodCliente = Str(r["COD_CLIENTE"]),
                Nombre     = Str(r["NOMBRE"])
            })
        .ContinueWith(t => (IEnumerable<PlnFiltroCliente>)t.Result);

    // ── SP_PLN_FILTRO_ASESORES ────────────────────────────────────────────────
    public Task<IEnumerable<PlnFiltroAsesor>> GetFiltroAsesoresAsync() =>
        GetCachedComboAsync(
            CacheKey("ASESORES"),
            "PKG_PLN.SP_PLN_FILTRO_ASESORES",
            r => new PlnFiltroAsesor
            {
                CodVende  = Str(r["COD_VENDE"]),
                Abreviada = Str(r["ABREVIADA"]),
                Nombre    = Str(r["NOMBRE"])
            })
        .ContinueWith(t => (IEnumerable<PlnFiltroAsesor>)t.Result);

    // ── SP_PLN_FILTRO_TITULOS ─────────────────────────────────────────────────
    public Task<IEnumerable<PlnFiltroTitulo>> GetFiltroTitulosAsync() =>
        GetCachedComboAsync(
            CacheKey("TITULOS"),
            "PKG_PLN.SP_PLN_FILTRO_TITULOS",
            r => new PlnFiltroTitulo
            {
                Titulo      = Str(r["TITULO"]),
                Descripcion = Str(r["DESCRIPCION"])
            })
        .ContinueWith(t => (IEnumerable<PlnFiltroTitulo>)t.Result);

    // ── SP_PLN_FILTRO_FIBRAS ──────────────────────────────────────────────────
    public Task<IEnumerable<PlnFiltroFibra>> GetFiltroFibrasAsync() =>
        GetCachedComboAsync(
            CacheKey("FIBRAS"),
            "PKG_PLN.SP_PLN_FILTRO_FIBRAS",
            r => new PlnFiltroFibra
            {
                TipoFibra   = Str(r["TIPO_FIBRA"]),
                Abreviado   = Str(r["ABREVIADO"]),
                Descripcion = Str(r["DESCRIPCION"])
            })
        .ContinueWith(t => (IEnumerable<PlnFiltroFibra>)t.Result);

    // ── SP_PLN_FILTRO_PROCESOS ────────────────────────────────────────────────
    public Task<IEnumerable<PlnFiltroProceso>> GetFiltroProcesosAsync() =>
        GetCachedComboAsync(
            CacheKey("PROCESOS"),
            "PKG_PLN.SP_PLN_FILTRO_PROCESOS",
            r => new PlnFiltroProceso
            {
                Proceso     = Str(r["PROCESO"]),
                Descripcion = Str(r["DESCRIPCION"])
            })
        .ContinueWith(t => (IEnumerable<PlnFiltroProceso>)t.Result);
}
