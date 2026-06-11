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

    /// <summary>
    /// Devuelve el valor de la primera columna encontrada en el reader.
    /// Permite fallback para cuando el SP en Oracle tiene un alias distinto al nuevo.
    /// </summary>
    private static object? Col(OracleDataReader r, params string[] names)
    {
        foreach (var name in names)
        {
            try   { return r[name]; }
            catch (IndexOutOfRangeException) { }
        }
        return DBNull.Value;
    }

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
                // Cols 1-4: Dimensiones de tiempo
                Mes                 = Str(r["MES"]),
                MesTex              = Str(r["MES_TEX"]),
                Ano                 = Str(r["ANO"]),
                Sem                 = Str(r["SEM"]),

                // Cols 5-12: Identificación del ítem
                Partida             = Str(r["PARTIDA"]),
                Cliente             = Str(r["CLIENTE"]),
                Material            = Str(r["MATERIAL"]),
                Est                 = Str(r["EST"]),
                Ne                  = Str(r["NE"]),
                Mat                 = Str(r["MAT"]),
                Lote                = Str(r["LOTE"]),
                FchPedido           = D(r["FCH_PEDIDO"]),

                // Cols 13-15: 1er Rodete (CONO UNO)
                EstimaRod           = D(r["ESTIMA_ROD"]),
                EntregRod           = D(r["ENTREG_ROD"]),
                DiasRod             = Dec(r["DIAS_ROD"]),

                // Cols 16-18: Material Hilandería
                EstimaMat           = D(r["ESTIMA_MAT"]),
                EntregMat           = D(r["ENTREG_MAT"]),
                DiasMh              = Dec(r["DIAS_MH"]),

                // Col 19: Fecha de la partida física
                FchaGuia            = D(r["FCHA_GUIA"]),

                // Cols 20-23: Receta TT
                EstimaReceta        = D(r["ESTIMA_RECETA"]),
                EntregReceta        = D(r["ENTREG_RECETA"]),
                DiasRec             = Dec(r["DIAS_REC"]),
                X                   = Dec(r["X"]),

                // Cols 24-28: Programa Tintorería
                FchPrograma         = D(r["FCH_PROGRAMA"]),
                MaqTen              = Str(r["MAQ_TEN"]),
                EstimaTenido        = D(r["ESTIMA_TENIDO"]),
                EntregTenido        = D(r["ENTREG_TENIDO"]),
                DiasTenido          = Dec(r["DIAS_TENIDO"]),

                // Cols 29-34: Fechas reales de producción
                // Col() con fallback: SP nuevo usa FCH_SEC_RODETE/FCH_SEC_MADEJA;
                // si Oracle aún tiene el alias viejo FCH_SECADO, se lee ése en su lugar.
                FchPartida          = D(r["FCH_PARTIDA"]),
                FchReceta           = D(r["FCH_RECETA"]),
                FchSecRodete        = D(Col(r, "FCH_SEC_RODETE", "FCH_SECADO")),
                FchSecMadeja        = D(Col(r, "FCH_SEC_MADEJA")),
                FchAprobCal         = D(r["FCH_APROB_CAL"]),
                TimeAprov           = Dec(r["TIME_APROV"]),

                // Cols 34-37: Acabado, enconado, revisado
                TipoAcabado         = Str(r["TIPO_ACABADO"]),
                FchEnconado         = D(r["FCH_ENCONADO"]),
                FchRevisado         = D(r["FCH_REVISADO"]),
                EvEncon             = Str(r["EV_ENCON"]),

                // Cols 38-42: Entrega y espera
                FchEntrega          = D(r["FCH_ENTREGA"]),
                IngAlmpt            = D(r["ING_ALMPT"]),
                DiasEnEspera        = Dec(r["DIAS_EN_ESPERA"]),
                De                  = Dec(r["DE"]),
                DeCopia             = Dec(r["DE_COPIA"]),

                // Cols 43-46: Kilogramos y tolerancia
                KgProg              = Dec(r["KG_PROG"]),
                KgDespa             = Dec(r["KG_DESPA"]),
                Gap                 = Dec(r["GAP"]),
                PctToleran          = Dec(r["PCT_TOLERAN"]),

                // Cols 47-48: Clasificaciones
                EstadoFlujo         = Str(r["ESTADO_FLUJO"]),
                EstadoDespacho      = Str(r["ESTADO_DESPACHO"]),

                // Cols 49-50: Apoyo
                AreaResponsable     = Str(r["AREA_RESPONSABLE"]),
                Bp                  = Str(r["BP"]),

                // Cols 51-66: Adicionales (no en DT Excel)
                PesoNeto            = Dec(r["PESO_NETO"]),
                Rmc                 = Str(r["RMC"]),
                NroRmc              = Str(r["NRO_RMC"]),
                Titulo              = Str(r["TITULO"]),
                TituloTexto         = Str(r["TITULO_TEXTO"]),
                Referencia          = Str(r["REFERENCIA"]),
                ProcesoTt           = Str(r["PROCESO_TT"]),
                PartMatiz           = Str(r["PART_MATIZ"]),
                EstEvaluacion       = Str(r["EST_EVALUACION"]),
                Defecto             = Str(r["DEFECTO"]),
                Resultado           = Str(r["RESULTADO"]),
                LaboVal             = Str(r["LABO_VAL"]),
                AcaMad              = Str(r["ACA_MAD"]),
                DiasRetraso         = Dec(r["DIAS_RETRASO"]),
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
