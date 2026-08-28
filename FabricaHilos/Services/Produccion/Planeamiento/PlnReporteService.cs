using System.Data;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
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
        FixMojibake(v == null || v == DBNull.Value ? null : v.ToString()?.Trim());

    // Correcciones puntuales para textos que llegan corruptos desde Oracle (encoding mal interpretado)
    // y que el algoritmo genérico de abajo no puede reconstruir de forma exacta.
    private static readonly Dictionary<string, string> KnownTextFixes = new(StringComparer.Ordinal)
    {
        ["EN ALMACÃ¿N"] = "EN ALMACÉN",
    };

    // Mapa inverso de los caracteres especiales de Windows-1252 (0x80-0x9F) a su byte original.
    private static readonly Dictionary<char, byte> Cp1252Extended = new()
    {
        ['\u20AC']=0x80, ['\u201A']=0x82, ['\u0192']=0x83, ['\u201E']=0x84, ['\u2026']=0x85,
        ['\u2020']=0x86, ['\u2021']=0x87, ['\u02C6']=0x88, ['\u2030']=0x89, ['\u0160']=0x8A,
        ['\u2039']=0x8B, ['\u0152']=0x8C, ['\u017D']=0x8E, ['\u2018']=0x91, ['\u2019']=0x92,
        ['\u201C']=0x93, ['\u201D']=0x94, ['\u2022']=0x95, ['\u2013']=0x96, ['\u2014']=0x97,
        ['\u02DC']=0x98, ['\u2122']=0x99, ['\u0161']=0x9A, ['\u203A']=0x9B, ['\u0153']=0x9C,
        ['\u017E']=0x9E, ['\u0178']=0x9F,
    };

    /// <summary>
    /// Repara textos que llegaron con "mojibake" (UTF-8 mal interpretado como Windows-1252),
    /// patrón típico visto en columnas de Oracle: "Ã‘" en vez de "Ñ", "Ã©" en vez de "é", etc.
    /// </summary>
    private static string? FixMojibake(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (KnownTextFixes.TryGetValue(s, out var known)) return known;
        if (s.IndexOf('Ã') < 0 && s.IndexOf('Â') < 0) return s;

        var bytes = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c <= 0xFF) bytes[i] = (byte)c;
            else if (Cp1252Extended.TryGetValue(c, out var b)) bytes[i] = b;
            else return s; // carácter fuera de rango: no es el mojibake esperado, no tocar
        }

        var repaired = Encoding.UTF8.GetString(bytes);
        return repaired.IndexOf('\uFFFD') < 0 && !string.Equals(repaired, s, StringComparison.Ordinal)
            ? repaired
            : s;
    }

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

                // Área responsable / motivo de retraso (combos) + descripción libre
                AreaResp            = Str(Col(r, "AREA_RESP")),
                MotivoRetraso       = Str(Col(r, "MOTIVO_RETRASO")),
                DescripcionMotivo   = Str(Col(r, "DESCRIPCION_MOTIVO")),

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

    // ── SP_PLN_CAT_MOTIVO_LISTA ───────────────────────────────────────────────
    public async Task<IEnumerable<PlnCatMotivo>> GetCatalogoMotivoAsync() =>
        await GetCachedComboAsync(
            CacheKey("CAT_MOTIVO"),
            "PKG_PLN.SP_PLN_CAT_MOTIVO_LISTA",
            r => new PlnCatMotivo
            {
                AreaResp = Str(r["AREA_RESP"]) ?? "",
                Motivo   = Str(r["MOTIVO"]) ?? "",
                Orden    = Convert.ToInt32(r["ORDEN"])
            });

    // -- SaveMotivo (AREA_RESP / MOTIVO / DESCRIPCION → PLN_ITEM_MOTIVO_RETRASO)
    public async Task SaveMotivoAsync(IEnumerable<PlnSaveMotivoDto> items, CancellationToken ct = default)
    {
        await using var conn = await AbrirConexionAsync();
        await using var tran = conn.BeginTransaction();
        try
        {
        foreach (var item in items)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = $"BEGIN {S}PKG_PLN.SP_PLN_UPD_ITEM_MOTIVO(" +
                ":p_numped,:p_nro,:p_det,:p_rep,:p_area,:p_motivo,:p_desc,NULL); END;";
            cmd.Parameters.Add("p_numped", OracleDbType.Decimal ).Value = (object?)item.NumPed    ?? DBNull.Value;
            cmd.Parameters.Add("p_nro",    OracleDbType.Decimal ).Value = (object?)item.Nro       ?? DBNull.Value;
            cmd.Parameters.Add("p_det",    OracleDbType.Decimal ).Value = (object?)item.NumDet    ?? DBNull.Value;
            cmd.Parameters.Add("p_rep",    OracleDbType.Varchar2).Value = (object?)item.Reproceso ?? DBNull.Value;
            cmd.Parameters.Add("p_area",   OracleDbType.Varchar2).Value = (object?)item.AreaResp   ?? DBNull.Value;
            cmd.Parameters.Add("p_motivo", OracleDbType.Varchar2).Value = (object?)item.Motivo     ?? DBNull.Value;
            cmd.Parameters.Add("p_desc",   OracleDbType.Varchar2).Value = (object?)item.Descripcion ?? DBNull.Value;
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

    // ── SP_PLN_INGRESO_PED_APROB_FIBRA ──────────────────────────────────────
    public async Task<PlnIngresoPedidoAprobadoFibraViewModel> GetIngresoPedidosAprobadosFibraAsync(
        DateTime fchIni,
        DateTime fchFin,
        CancellationToken ct = default)
    {
        var vm = new PlnIngresoPedidoAprobadoFibraViewModel { FchIni = fchIni, FchFin = fchFin };

        await using var conn = await AbrirConexionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_INGRESO_PED_APROB_FIBRA";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = ReportTimeoutSeconds;

        cmd.Parameters.Add("p_fch_ini", OracleDbType.Date).Value = fchIni;
        cmd.Parameters.Add("p_fch_fin", OracleDbType.Date).Value = fchFin;

        var pProd      = cmd.Parameters.Add("p_cursor_prod",      OracleDbType.RefCursor);
        pProd.Direction = ParameterDirection.Output;
        var pDespacho  = cmd.Parameters.Add("p_cursor_despacho",  OracleDbType.RefCursor);
        pDespacho.Direction = ParameterDirection.Output;
        var pServicios = cmd.Parameters.Add("p_cursor_servicios", OracleDbType.RefCursor);
        pServicios.Direction = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync(ct);

        List<PlnIngresoFibraItem> LeerCursor(OracleRefCursor cursor)
        {
            var lista = new List<PlnIngresoFibraItem>();
            using var r = cursor.GetDataReader();
            while (r.Read())
            {
                lista.Add(new PlnIngresoFibraItem
                {
                    Orden   = Str(r["ORDEN"])   ?? "",
                    Cliente = Str(r["CLIENTE"]) ?? "",
                    Grupo   = Str(r["GRUPO"])   ?? "",
                    Tipo    = Str(r["TIPO"])    ?? "",
                    Kg      = Dec(r["KG"])      ?? 0m,
                });
            }
            return lista;
        }

        vm.Produccion   = LeerCursor((OracleRefCursor)pProd.Value);
        vm.SoloDespacho = LeerCursor((OracleRefCursor)pDespacho.Value);
        vm.Servicios    = LeerCursor((OracleRefCursor)pServicios.Value);

        return vm;
    }
}