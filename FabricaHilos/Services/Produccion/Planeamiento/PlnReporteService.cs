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
                // Col 0: Color de fila
                ColorHexa           = Str(Col(r, "COLORHEXA")),

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
                XRod                = Dec(Col(r, "X_ROD")),

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
                XTenido             = Dec(Col(r, "X_TENIDO")),

                // Cols 29-34: Fechas reales de producción
                // Col() con fallback: SP nuevo usa FCH_SEC_RODETE/FCH_SEC_MADEJA;
                // si Oracle aún tiene el alias viejo FCH_SECADO, se lee ése en su lugar.
                FchPartida          = D(r["FCH_PARTIDA"]),
                FchReceta           = D(r["FCH_RECETA"]),
                FchSecRodete        = D(Col(r, "FCH_SEC_RODETE", "FCH_SECADO")),
                FchSecMadeja        = D(Col(r, "FCH_SEC_MADEJA")),
                FchAprobCal         = D(r["FCH_APROB_CAL"]),
                TimeAprov           = Dec(r["TIME_APROV"]),

                // Cols 34-38: Acabado, enconado, revisado
                TipoAcabado         = Str(r["TIPO_ACABADO"]),
                Acabado             = Str(Col(r, "ACABADO")),
                FchEnconado         = D(r["FCH_ENCONADO"]),
                FchRevisado         = D(r["FCH_REVISADO"]),
                EvEncon             = Str(r["EV_ENCON"]),
                Calificacion        = Str(Col(r, "CALIFICACION")),

                // Cols 38-42: Entrega y espera
                FchEntrega          = D(r["FCH_ENTREGA"]),
                IngAlmpt            = D(r["ING_ALMPT"]),
                DiasEnEspera        = Dec(r["DIAS_EN_ESPERA"]),
                De                  = Dec(r["DE"]),
                DeCopia             = Dec(r["DE_COPIA"]),

                // Cols 43-47: Kilogramos y tolerancia
                KgPedido            = Dec(Col(r, "KG_PEDIDO")),
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

                // Campos clave (edit/save)
                NroProg             = Dec(Col(r, "NROPROG_DET")),
                NumPedKey           = Dec(Col(r, "NUM_PED_KEY")),
                NroKey              = Dec(Col(r, "NRO_KEY")),
                NumDetKey           = Dec(Col(r, "NUM_DET_KEY")),
                ReprocesoKey        = Str(Col(r, "REPROCESO_KEY")),
                FchProgKey          = D(Col(r, "FCH_PROG_KEY")),
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
    public async Task<IEnumerable<PlnFiltroCliente>> GetFiltroClientesAsync() =>
        await GetCachedComboAsync(
            CacheKey("CLIENTES"),
            "PKG_PLN.SP_PLN_FILTRO_CLIENTES",
            r => new PlnFiltroCliente
            {
                CodCliente = Str(r["COD_CLIENTE"]),
                Nombre     = Str(r["NOMBRE"])
            });

    // ── SP_PLN_FILTRO_ASESORES ────────────────────────────────────────────────
    public async Task<IEnumerable<PlnFiltroAsesor>> GetFiltroAsesoresAsync() =>
        await GetCachedComboAsync(
            CacheKey("ASESORES"),
            "PKG_PLN.SP_PLN_FILTRO_ASESORES",
            r => new PlnFiltroAsesor
            {
                CodVende  = Str(r["COD_VENDE"]),
                Abreviada = Str(r["ABREVIADA"]),
                Nombre    = Str(r["NOMBRE"])
            });

    // ── SP_PLN_FILTRO_TITULOS ─────────────────────────────────────────────────
    public async Task<IEnumerable<PlnFiltroTitulo>> GetFiltroTitulosAsync() =>
        await GetCachedComboAsync(
            CacheKey("TITULOS"),
            "PKG_PLN.SP_PLN_FILTRO_TITULOS",
            r => new PlnFiltroTitulo
            {
                Titulo      = Str(r["TITULO"]),
                Descripcion = Str(r["DESCRIPCION"])
            });

    // ── SP_PLN_FILTRO_FIBRAS ──────────────────────────────────────────────────
    public async Task<IEnumerable<PlnFiltroFibra>> GetFiltroFibrasAsync() =>
        await GetCachedComboAsync(
            CacheKey("FIBRAS"),
            "PKG_PLN.SP_PLN_FILTRO_FIBRAS",
            r => new PlnFiltroFibra
            {
                TipoFibra   = Str(r["TIPO_FIBRA"]),
                Abreviado   = Str(r["ABREVIADO"]),
                Descripcion = Str(r["DESCRIPCION"])
            });

    // ── SP_PLN_FILTRO_PROCESOS ────────────────────────────────────────────────
    public async Task<IEnumerable<PlnFiltroProceso>> GetFiltroProcesosAsync() =>
        await GetCachedComboAsync(
            CacheKey("PROCESOS"),
            "PKG_PLN.SP_PLN_FILTRO_PROCESOS",
            r => new PlnFiltroProceso
            {
                Proceso     = Str(r["PROCESO"]),
                Descripcion = Str(r["DESCRIPCION"])
            });

    // Centinela que el SP interpreta como "poner NULL explícitamente".
    // Necesario porque Oracle trata '' como NULL, por lo que no sirve como
    // distinción entre "no envío este campo" (NULL) y "quiero borrarlo" ('').
    private const string ClearSentinel = "__CLEAR__";

    // -- SaveColorHexa
    public async Task SaveColorHexaAsync(IEnumerable<PlnSaveColorDto> items, CancellationToken ct = default)
    {
        await using var conn = await AbrirConexionAsync();
        await using var tran = conn.BeginTransaction();
        try
        {
        foreach (var item in items)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = $"BEGIN {S}PKG_PLN.SP_PLN_UPD_ITEM_OBS_COLOR(" +
                ":p_nroprog,:p_numped,:p_nro,:p_det,:p_rep,:p_fchprog,NULL,:p_color,NULL); END;";
            cmd.Parameters.Add("p_nroprog",  OracleDbType.Decimal  ).Value = (object?)item.NroProg   ?? DBNull.Value;
            cmd.Parameters.Add("p_numped",   OracleDbType.Decimal  ).Value = (object?)item.NumPed    ?? DBNull.Value;
            cmd.Parameters.Add("p_nro",      OracleDbType.Decimal  ).Value = (object?)item.Nro       ?? DBNull.Value;
            cmd.Parameters.Add("p_det",      OracleDbType.Decimal  ).Value = (object?)item.NumDet    ?? DBNull.Value;
            cmd.Parameters.Add("p_rep",      OracleDbType.Varchar2 ).Value = (object?)item.Reproceso ?? DBNull.Value;
            cmd.Parameters.Add("p_fchprog",  OracleDbType.Date     ).Value = (object?)item.FchProg   ?? DBNull.Value;
            // null = quitar etiqueta → centinela __CLEAR__ para que SP borre el campo
            cmd.Parameters.Add("p_color",    OracleDbType.Varchar2 ).Value = item.ColorHexa ?? ClearSentinel;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        tran.Commit();
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    // -- SaveObservacion
    public async Task SaveObservacionAsync(IEnumerable<PlnSaveObsDto> items, CancellationToken ct = default)
    {
        await using var conn = await AbrirConexionAsync();
        await using var tran = conn.BeginTransaction();
        try
        {
        // Sin filtro: texto vacío también se envía con centinela para borrar el campo
        foreach (var item in items)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = $"BEGIN {S}PKG_PLN.SP_PLN_UPD_ITEM_OBS_COLOR(" +
                ":p_nroprog,:p_numped,:p_nro,:p_det,:p_rep,:p_fchprog,:p_obs,NULL,NULL); END;";
            cmd.Parameters.Add("p_nroprog",  OracleDbType.Decimal  ).Value = (object?)item.NroProg   ?? DBNull.Value;
            cmd.Parameters.Add("p_numped",   OracleDbType.Decimal  ).Value = (object?)item.NumPed    ?? DBNull.Value;
            cmd.Parameters.Add("p_nro",      OracleDbType.Decimal  ).Value = (object?)item.Nro       ?? DBNull.Value;
            cmd.Parameters.Add("p_det",      OracleDbType.Decimal  ).Value = (object?)item.NumDet    ?? DBNull.Value;
            cmd.Parameters.Add("p_rep",      OracleDbType.Varchar2 ).Value = (object?)item.Reproceso ?? DBNull.Value;
            cmd.Parameters.Add("p_fchprog",  OracleDbType.Date     ).Value = (object?)item.FchProg   ?? DBNull.Value;
            // texto vacío = borrar → centinela __CLEAR__
            cmd.Parameters.Add("p_obs",      OracleDbType.Varchar2 ).Value =
                string.IsNullOrWhiteSpace(item.Observaciones) ? ClearSentinel : item.Observaciones!.Trim();
            await cmd.ExecuteNonQueryAsync(ct);
        }
        tran.Commit();
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }
}