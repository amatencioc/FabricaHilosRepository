using FabricaHilos.Models.Sire;
using System.IO.Compression;
using System.Text;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Genera el archivo ZIP con el TXT de propuesta en formato SUNAT real
/// a partir de los registros de SIG.SIRE_PROPUESTA.
/// El TXT generado es idéntico en estructura al que SUNAT entrega al exportar la propuesta,
/// por lo que puede usarse para validar el contenido antes del envío real.
/// Formato: pipe-delimited UTF-8 sin BOM, CRLF.
/// </summary>
public sealed class SirePropuestaZipService
{
    private readonly ISireOracleRepository _repo;
    private readonly ILogger<SirePropuestaZipService> _logger;

    public SirePropuestaZipService(ISireOracleRepository repo, ILogger<SirePropuestaZipService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    /// <summary>
    /// Genera el ZIP con TXT de propuesta SIRE para el período y tipo dados.
    /// El nombre del archivo ZIP sigue el estándar SUNAT: {RUC}-{YYYYMMDD}-{HHMMSS}-propuesta.zip
    /// El TXT interior se llama: LE{RUC}{YYYYMM}00{CodLibro}1.txt
    /// </summary>
    /// <param name="tipo">"ventas" o "compras"</param>
    /// <param name="periodo">YYYYMM</param>
    /// <param name="ruc">RUC del contribuyente</param>
    public async Task<(byte[] ZipBytes, string NombreZip, string NombreTxt)> GenerarAsync(
        string tipo, string periodo, string ruc, CancellationToken ct = default)
    {
        var registros = await _repo.GetRegistrosPropuestaAsync(tipo, periodo, ct);
        _logger.LogInformation("[SIRE-ZIP] Generando ZIP local: tipo={Tipo} periodo={Periodo} registros={N}", tipo, periodo, registros.Count);

        var esVentas  = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase);
        // Código de libro: RVIE=140000, RCE=080000
        var codLibro  = esVentas ? "140000" : "080000";
        var ts        = DateTime.Now;
        var nombreZip = $"{ruc}-{ts:yyyyMMdd}-{ts:HHmmss}-propuesta.zip";
        // Nombre TXT estándar SUNAT para propuesta: LE{RUC}{PERIODO}00{CodLibro}1.txt
        var nombreTxt = $"LE{ruc}{periodo}00{codLibro}1.txt";

        var txtBytes = GenerarTxt(registros, esVentas);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(nombreTxt, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(txtBytes, ct);
        }

        _logger.LogInformation("[SIRE-ZIP] ZIP generado: {NombreZip} ({Bytes} bytes, {Lineas} líneas)",
            nombreZip, ms.Length, registros.Count);

        return (ms.ToArray(), nombreZip, nombreTxt);
    }

    /// <summary>
    /// Construye el TXT pipe-delimited según el formato SUNAT.
    /// Formato RCE (compras), 38 columnas:
    ///   [0]  CarSunat  [1] FEmision  [2] FVencto  [3] TipDoc  [4] Serie
    ///   [5]  AnioDam   [6] Numero    [7] NroFin   [8] TdocId  [9] Ruc
    ///   [10] Nombre    [11] BiGravDg [12] IgvIpmDg [13] BiGravDgng [14] IgvIpmDgng
    ///   [15] BiGravDng [16] IgvIpmDng [17] ValAdqNg [18] Isc  [19] Icbper
    ///   [20] OtrosTrib [21] TotalCp  [22] Moneda  [23] Cambio
    ///   [24] FDocref   [25] TipDocref [26] SerDocref [27] CodDam [28] NroDocref
    ///   [29] TipoBien  [30] IdProyecto [31] Porcpart [32] Imb
    ///   [33] CarMod    [34] FlagDetrac [35] TipoNota [36] EstComp [37] Inconsist
    /// Formato RVIE (ventas), 32 columnas (sin AnioDam, sin cols de compras específicas).
    /// </summary>
    private static byte[] GenerarTxt(List<SireValidaRegistro> registros, bool esVentas)
    {
        static string F(string? s) => s ?? string.Empty;
        static string D(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy") : string.Empty;
        static string N(decimal v) => v == 0m ? string.Empty : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        foreach (var r in registros)
        {
            string linea;
            if (!esVentas)
            {
                // RCE: 38 campos
                linea = string.Join("|",
                    F(r.CarSunat),          // 0
                    D(r.FEmision),          // 1
                    D(r.FVencto),           // 2
                    F(r.Tipdoc),            // 3
                    F(r.Serie),             // 4
                    F(r.AnioDam),           // 5
                    F(r.Numero),            // 6
                    F(r.Nrofin),            // 7
                    F(r.Tdocid),            // 8
                    F(r.Ruc),               // 9
                    F(r.Nombre),            // 10
                    N(r.BiGravDg),          // 11
                    N(r.IgvIpmDg),          // 12
                    N(r.BiGravDgng),        // 13
                    N(r.IgvIpmDgng),        // 14
                    N(r.BiGravDng),         // 15
                    N(r.IgvIpmDng),         // 16
                    N(r.ValAdqNg),          // 17
                    N(r.Isc),               // 18
                    N(r.Icbper),            // 19
                    N(r.OtrosTrib),         // 20
                    N(r.TotalCp),           // 21
                    F(r.Moneda),            // 22
                    N(r.Cambio),            // 23
                    D(r.FDocref),           // 24
                    F(r.TipDocref),         // 25
                    F(r.SerDocref),         // 26
                    F(r.CodDam),            // 27
                    F(r.NroDocref),         // 28
                    F(r.TipoBien),          // 29
                    F(r.IdProyecto),        // 30
                    N(r.Porcpart),          // 31
                    N(r.Imb),               // 32
                    F(r.CarMod),            // 33
                    F(r.FlagDetrac),        // 34
                    F(r.TipoNota),          // 35
                    F(r.EstComp) == string.Empty ? "1" : F(r.EstComp),  // 36
                    F(r.Inconsist) == string.Empty ? "0" : F(r.Inconsist) // 37
                );
            }
            else
            {
                // RVIE: 32 campos (sin AnioDam, sin CodDam, TipoBien, Porcpart, Imb, CarMod, FlagDetrac)
                linea = string.Join("|",
                    F(r.CarSunat),          // 0
                    D(r.FEmision),          // 1
                    D(r.FVencto),           // 2
                    F(r.Tipdoc),            // 3
                    F(r.Serie),             // 4
                    F(r.Numero),            // 5
                    F(r.Nrofin),            // 6
                    F(r.Tdocid),            // 7
                    F(r.Ruc),               // 8
                    F(r.Nombre),            // 9
                    N(r.BiGravDg),          // 10 (biGravDg = BI gravada)
                    string.Empty,           // 11 DsctoBI (skip)
                    N(r.IgvIpmDg),          // 12
                    N(r.Isc),               // 13 (ventas: Isc en pos 17→mapeado a 13 aquí)
                    string.Empty,           // 14 IVAP
                    string.Empty,           // 15 OP gravada
                    string.Empty,           // 16 OP exonerada
                    string.Empty,           // 17 OP inafecta
                    string.Empty,           // 18 ISC2
                    N(r.OtrosTrib),         // 19
                    N(r.Icbper),            // 20
                    N(r.TotalCp),           // 21
                    F(r.Moneda),            // 22
                    N(r.Cambio),            // 23
                    D(r.FDocref),           // 24
                    F(r.TipDocref),         // 25
                    F(r.SerDocref),         // 26
                    F(r.NroDocref),         // 27
                    F(r.TipoNota),          // 28
                    F(r.EstComp) == string.Empty ? "1" : F(r.EstComp),  // 29
                    F(r.Inconsist) == string.Empty ? "0" : F(r.Inconsist) // 30
                );
            }
            sb.Append(linea).Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }


    // ─────────────────────────────────────────────────────────────────────
    // GENERACION DESDE SIRE_LEGACY (para validador / reemplazo en SUNAT)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>

    // ─────────────────────────────────────────────────────────────────────
    // GENERACION DESDE SIRE_LEGACY — propuesta de REEMPLAZO para SUNAT
    // Estructura validada contra SireValidaService.ParsearLinea (fuente autoritativa)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Genera el ZIP de REEMPLAZO de propuesta usando los datos del ERP (SIRE_LEGACY).
    ///
    /// NOMBRE TXT  : RCE  -> LE{RUC}{YYYY}{MM}00080400021112.txt
    ///               RVIE -> LE{RUC}{YYYY}{MM}00140400011112.txt
    /// NOMBRE ZIP  : mismo nombre base que el TXT, solo extensión .zip
    ///               RCE  -> LE{RUC}{YYYY}{MM}00080400021112.zip
    ///               RVIE -> LE{RUC}{YYYY}{MM}00140400011112.zip
    ///
    /// ESTRUCTURA TXT (pipe-delimited, UTF-8 sin BOM, CRLF):
    ///   Prefijo 3 campos fijos: RUC-empresa | RazonSocial | Periodo(YYYYMM)
    ///   RCE:  41 campos totales (3 prefijo + 38 datos)
    ///   RVIE: 35 campos totales (3 prefijo + 32 datos)
    /// </summary>
    public async Task<(byte[] ZipBytes, string NombreZip, string NombreTxt)> GenerarDesdeLegacyAsync(
        string tipo, string periodo, string ruc, string razonSocial, CancellationToken ct = default)
    {
        var registros = await _repo.GetLegacyAsync(tipo, periodo, ct);
        registros = registros.Where(r => r.Anulado != "S").ToList();

        _logger.LogInformation("[SIRE-ZIP-LEGACY] tipo={Tipo} periodo={Periodo} ruc={Ruc} registros={N}",
            tipo, periodo, ruc, registros.Count);

        // Advertencia preventiva: notas de crédito/débito sin documento referenciado
        // generarán el error paramétrico 428 en SUNAT. Deben completarse en SIRE_LEGACY
        // (TIP_DOCREF, SER_DOCREF, NRO_DOCREF, F_DOCREF) antes de enviar.
        var notasSinRef = registros
            .Where(r => (r.Tipdoc == "07" || r.Tipdoc == "08" || r.Tipdoc == "87" || r.Tipdoc == "88")
                        && string.IsNullOrEmpty(r.TipDocref))
            .ToList();
        if (notasSinRef.Count > 0)
        {
            foreach (var nr in notasSinRef)
                _logger.LogWarning(
                    "[SIRE-ZIP-LEGACY] NC/ND sin doc referenciado (error 428): tipo={T} serie={S} numero={N} rucProv={R} — completar TIP_DOCREF/SER_DOCREF/NRO_DOCREF/F_DOCREF en SIRE_LEGACY",
                    nr.Tipdoc, nr.Serie, nr.Numero, nr.Ruc);
            _logger.LogWarning("[SIRE-ZIP-LEGACY] {C} nota(s) sin doc referenciado — se incluirán pero SUNAT rechazará con error 428", notasSinRef.Count);
        }

        var esVentas = tipo.Equals("ventas", StringComparison.OrdinalIgnoreCase);
        var yyyy     = periodo.Length == 6 ? periodo[..4] : periodo;
        var mm       = periodo.Length == 6 ? periodo[4..] : "01";

        // Nombre TXT: SUNAT no acepta guiones dentro del nombre del TXT
        //   RCE  -> LE{RUC}{YYYY}{MM}00080400021112.txt
        //   RVIE -> LE{RUC}{YYYY}{MM}00140400011112.txt
        var sufTxt    = esVentas ? "00140400011112" : "00080400021112";
        var nombreTxt = $"LE{ruc}{yyyy}{mm}{sufTxt}.txt";

        // Nombre ZIP: mismo nombre base que el TXT, solo extensión .zip
        var nombreZip = Path.ChangeExtension(nombreTxt, ".zip");

        var txtBytes = GenerarTxtDesdeLegacy(registros, esVentas, ruc, razonSocial, periodo);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(nombreTxt, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(txtBytes, ct);
        }

        _logger.LogInformation("[SIRE-ZIP-LEGACY] Generado: zip={Zip} | txt={Txt} | {Bytes} bytes | {N} lineas",
            nombreZip, nombreTxt, ms.Length, registros.Count);

        return (ms.ToArray(), nombreZip, nombreTxt);
    }

    /// <summary>
    /// Genera el TXT pipe-delimited desde SIRE_LEGACY en el formato exacto que SUNAT acepta
    /// para el reemplazo de propuesta.
    ///
    /// PREFIJO (3 campos identicos en todas las lineas):
    ///   [0] RUC empresa
    ///   [1] Razon social empresa
    ///   [2] Periodo YYYYMM (numerico)
    ///
    /// RCE (compras) — 38 campos de datos (41 total con prefijo):
    ///   [o+0]  CAR_SUNAT  — correlativo 10 digitos
    ///   [o+1]  F_EMISION  — dd/MM/yyyy
    ///   [o+2]  F_VENCTO   — dd/MM/yyyy
    ///   [o+3]  TIPDOC
    ///   [o+4]  SERIE
    ///   [o+5]  ANIO_DAM   — solo tipos 50/52
    ///   [o+6]  NUMERO
    ///   [o+7]  NRO_FINAL  — vacio
    ///   [o+8]  TDOCID
    ///   [o+9]  RUC proveedor
    ///   [o+10] NOMBRE proveedor
    ///   [o+11] BI_GRAV_DG
    ///   [o+12] IGV_DG
    ///   [o+13] BI_GRAV_DGNG — 0 fijo
    ///   [o+14] IGV_DGNG     — 0 fijo
    ///   [o+15] BI_GRAV_DNG  — 0 fijo
    ///   [o+16] IGV_DNG      — 0 fijo
    ///   [o+17] VAL_ADQ_NG
    ///   [o+18] ISC
    ///   [o+19] ICBPER        — 0 fijo
    ///   [o+20] OTROS_TRIB
    ///   [o+21] TOTAL_CP
    ///   [o+22] MONEDA
    ///   [o+23] TIPO_CAMBIO  — vacio si PEN, 3 dec si USD
    ///   [o+24] F_DOCREF
    ///   [o+25] TIP_DOCREF
    ///   [o+26] SER_DOCREF
    ///   [o+27] COD_DAM      — vacio
    ///   [o+28] NRO_DOCREF
    ///   [o+29] TIPO_BIEN    — de SIRE_LEGACY.TIPO_BIEN; default "1" si NULL
    ///   [o+30] ID_PROYECTO  — vacio
    ///   [o+31] PORCPART     — vacio
    ///   [o+32] IMB          — vacio
    ///   [o+33] CAR_MOD      — vacio
    ///   [o+34] FLAG_DETRAC  — "D" o vacio
    ///   [o+35] TIPO_NOTA    — vacio (no aplica compras)
    ///   [o+36] EST_COMP     — vacio (reemplazo: SUNAT lo recalcula)
    ///   [o+37] INCONSIST    — vacio (reemplazo: SUNAT lo recalcula)
    ///
    /// RVIE (ventas) — 32 campos de datos (35 total con prefijo):
    ///   [o+0..o+4] = CAR,FEmision,FVencto,TipDoc,Serie
    ///   [o+5]  NUMERO
    ///   [o+6]  NRO_FINAL   — vacio
    ///   [o+7]  TDOCID
    ///   [o+8]  RUC cliente
    ///   [o+9]  NOMBRE cliente
    ///   [o+10] VALOR_EXPORT — vacio
    ///   [o+11] BI_GRAV
    ///   [o+12] DSCTO_BI    — vacio
    ///   [o+13] IGV
    ///   [o+14] DSCTO_IGV   — vacio
    ///   [o+15] MTO_EXONERADO — vacio
    ///   [o+16] MTO_INAFECTO  — vacio
    ///   [o+17] ISC
    ///   [o+18] BI_GRAV_IVAP — vacio
    ///   [o+19] IVAP          — vacio
    ///   [o+20] ICBPER        — vacio
    ///   [o+21] OTROS_TRIB
    ///   [o+22] TOTAL_CP
    ///   [o+23] MONEDA
    ///   [o+24] TIPO_CAMBIO
    ///   [o+25] F_DOCREF
    ///   [o+26] TIP_DOCREF
    ///   [o+27] SER_DOCREF
    ///   [o+28] NRO_DOCREF
    ///   [o+29] ID_PROYECTO — vacio
    ///   [o+30] TIPO_NOTA
    ///   [o+31] EST_COMP    — "1"
    ///
    /// Reglas de formato:
    ///   Importes : "0.00" (2 dec fijos), vacio si cero
    ///   Cambio   : "0.000" (3 dec), SOLO si moneda != PEN
    ///   Fechas   : "dd/MM/yyyy", vacio si null
    /// </summary>
    private static byte[] GenerarTxtDesdeLegacy(
        List<SireLegacyRegistro> registros, bool esVentas,
        string ruc, string razonSocial, string periodo)
    {
        static string F(string? s)   => s ?? string.Empty;
        // NOMBRE: elimina pipes, caracteres de control y trunca a 100 chars (SUNAT campo 14 error 403)
        static string N100(string? s) =>
            s is null ? string.Empty
            : new string(s.Where(c => c != '|' && c >= ' ').ToArray())
              .Trim()
              [..Math.Min(100, s.Length > 0 ? s.Length : 0)];
        static string D(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy") : string.Empty;
        static string NR(decimal v)  => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        static string TC(string? moneda, decimal cambio) =>
            string.IsNullOrEmpty(moneda) || moneda == "PEN"
                ? string.Empty
                : cambio.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        // NUMERO: para tipos físicos y electrónicos SUNAT exige solo dígitos sin padding especial
        // Para DUAs (tipo 50/52) el numero puede tener formato libre, pero el campo 14=NOMBRE
        // seguía fallando; el problema es el nombre con caracteres especiales.
        static string NumCp(string? num) =>
            string.IsNullOrEmpty(num) ? string.Empty : num.Trim();
        // NRO_DOCREF: para tipos 07/08/87/88 SUNAT exige el número de referencia (campo 32 error 402+428)
        static string NroRef(string? nro) =>
            string.IsNullOrEmpty(nro) ? string.Empty : nro.Trim();
        // Es nota de crédito/débito: tipos 07,08,87,88 -> campos 21-24 deben ser negativos (error 425)
        static bool EsNota(string? tip) =>
            tip == "07" || tip == "08" || tip == "87" || tip == "88";

        // Prefijo: sanitizar razon social (eliminar pipes y control chars que rompen el delimitador)
        var razonSan = new string(razonSocial?.Where(c => c != '|' && c >= ' ').ToArray() ?? []);
        var prefijo = $"{ruc}|{razonSan}|{periodo}";

        var sb = new StringBuilder();
        var correlativo = 0;

        foreach (var r in registros)
        {
            correlativo++;
            var car = correlativo.ToString().PadLeft(10, '0');
            string datos;

            if (!esVentas)
            {
                // RCE: 38 campos de datos (SUNAT exige 0.00 en importes, nunca vacio)
                var tipoBien = string.IsNullOrWhiteSpace(r.TipoBien) ? "1" : F(r.TipoBien);
                var esNota   = EsNota(r.Tipdoc);

                // F_VENCTO (campo 6 error 414): para servicios (tipo 14) y DUA (tipo 50/52)
                // SUNAT requiere fecha de vencimiento; si no la tiene, usar F_EMISION como fallback.
                var fVencto = r.FVencto.HasValue ? D(r.FVencto)
                              : (r.Tipdoc == "14" || r.Tipdoc == "50" || r.Tipdoc == "52")
                                ? D(r.FEmision)
                                : string.Empty;

                // ANIO_DAM (campo 9 error 401): para DUAs (tipo 50/52) SUNAT exige el año.
                // Si viene vacio, tomarlo del año de la fecha de emisión.
                var anioDam = F(r.AnioDam);
                if (string.IsNullOrEmpty(anioDam) && (r.Tipdoc == "50" || r.Tipdoc == "52")
                    && r.FEmision.HasValue)
                    anioDam = r.FEmision.Value.Year.ToString();

                // Valores para notas de crédito/débito (campos 21-24 error 425):
                // SUNAT exige que VAL_ADQ_NG, ISC, ICBPER y OTROS_TRIB sean negativos en NC/ND.
                var valAdqNg  = esNota ? -Math.Abs(r.ValAdqNg)  : r.ValAdqNg;
                var isc       = esNota ? -Math.Abs(r.Isc)        : r.Isc;
                var otros     = esNota ? -Math.Abs(r.Otros)      : r.Otros;

                datos = string.Join("|",
                    car,                    // [o+0]  CAR_SUNAT
                    D(r.FEmision),          // [o+1]  F_EMISION
                    fVencto,                // [o+2]  F_VENCTO  (fallback para tipo 14 y 50)
                    F(r.Tipdoc),            // [o+3]  TIPDOC
                    F(r.Serie),             // [o+4]  SERIE
                    anioDam,                // [o+5]  ANIO_DAM  (obligatorio para tipos 50/52)
                    NumCp(r.Numero),        // [o+6]  NUMERO
                    string.Empty,           // [o+7]  NRO_FINAL (vacio)
                    F(r.Tdocid),            // [o+8]  TDOCID
                    F(r.Ruc),               // [o+9]  RUC proveedor
                    N100(r.Nombre),         // [o+10] NOMBRE proveedor (saneado y truncado)
                    NR(r.BaseImponible),    // [o+11] BI_GRAV_DG
                    NR(r.Igv),              // [o+12] IGV_DG
                    "0.00",                 // [o+13] BI_GRAV_DGNG
                    "0.00",                 // [o+14] IGV_DGNG
                    "0.00",                 // [o+15] BI_GRAV_DNG
                    "0.00",                 // [o+16] IGV_DNG
                    NR(valAdqNg),           // [o+17] VAL_ADQ_NG (negativo en NC/ND)
                    NR(isc),                // [o+18] ISC        (negativo en NC/ND)
                    "0.00",                 // [o+19] ICBPER
                    NR(otros),              // [o+20] OTROS_TRIB (negativo en NC/ND)
                    NR(r.Total),            // [o+21] TOTAL_CP
                    F(r.Moneda),            // [o+22] MONEDA
                    TC(r.Moneda, r.Cambio), // [o+23] TIPO_CAMBIO
                    D(r.FDocref),           // [o+24] F_DOCREF
                    F(r.TipDocref),         // [o+25] TIP_DOCREF
                    F(r.SerDocref),         // [o+26] SER_DOCREF
                    string.Empty,           // [o+27] COD_DAM      (vacio siempre)
                    NroRef(r.NroDocref),    // [o+28] NRO_DOCREF  (trimmed; obligatorio en 07/08)
                    tipoBien,               // [o+29] TIPO_BIEN
                    esNota ? "0" : string.Empty, // [o+30] ID_PROYECTO (SUNAT exige "0" en 07/08/87/88)
                    string.Empty,           // [o+31] PORCPART
                    esNota ? "0" : string.Empty, // [o+32] IMB         (SUNAT exige "0" en 07/08/87/88)
                    string.Empty,           // [o+33] CAR_MOD
                    F(r.FlagDetrac),        // [o+34] FLAG_DETRAC  ("D" o vacio)
                    string.Empty,           // [o+35] TIPO_NOTA    (no aplica compras)
                    string.Empty,           // [o+36] EST_COMP     (vacio en reemplazo)
                    string.Empty            // [o+37] INCONSIST    (vacio en reemplazo)
                );
            }
            else
            {
                // RVIE: 32 campos de datos
                var esNotaV = EsNota(r.Tipdoc);
                var iscV    = esNotaV ? -Math.Abs(r.Isc)   : r.Isc;
                var otrosV  = esNotaV ? -Math.Abs(r.Otros) : r.Otros;
                datos = string.Join("|",
                    car,                    // [o+0]  CAR_SUNAT
                    D(r.FEmision),          // [o+1]  F_EMISION
                    D(r.FVencto),           // [o+2]  F_VENCTO
                    F(r.Tipdoc),            // [o+3]  TIPDOC
                    F(r.Serie),             // [o+4]  SERIE
                    NumCp(r.Numero),        // [o+5]  NUMERO
                    string.Empty,           // [o+6]  NRO_FINAL   (vacio)
                    F(r.Tdocid),            // [o+7]  TDOCID
                    F(r.Ruc),               // [o+8]  RUC cliente
                    N100(r.Nombre),         // [o+9]  NOMBRE cliente (saneado y truncado)
                    string.Empty,           // [o+10] VALOR_EXPORT (vacio)
                    NR(r.BaseImponible),    // [o+11] BI_GRAV
                    string.Empty,           // [o+12] DSCTO_BI    (vacio)
                    NR(r.Igv),              // [o+13] IGV
                    string.Empty,           // [o+14] DSCTO_IGV   (vacio)
                    string.Empty,           // [o+15] MTO_EXONERADO (vacio)
                    string.Empty,           // [o+16] MTO_INAFECTO  (vacio)
                    NR(iscV),               // [o+17] ISC         (negativo en NC/ND)
                    string.Empty,           // [o+18] BI_GRAV_IVAP (vacio)
                    string.Empty,           // [o+19] IVAP          (vacio)
                    string.Empty,           // [o+20] ICBPER        (vacio ventas)
                    NR(otrosV),             // [o+21] OTROS_TRIB  (negativo en NC/ND)
                    NR(r.Total),            // [o+22] TOTAL_CP
                    F(r.Moneda),            // [o+23] MONEDA
                    TC(r.Moneda, r.Cambio), // [o+24] TIPO_CAMBIO
                    D(r.FDocref),           // [o+25] F_DOCREF
                    F(r.TipDocref),         // [o+26] TIP_DOCREF
                    F(r.SerDocref),         // [o+27] SER_DOCREF
                    NroRef(r.NroDocref),    // [o+28] NRO_DOCREF  (trimmed)
                    string.Empty,           // [o+29] ID_PROYECTO  (vacio)
                    F(r.TipoNota),          // [o+30] TIPO_NOTA
                    string.Empty            // [o+31] EST_COMP     (vacio en reemplazo)
                );
            }

            sb.Append(prefijo).Append('|').Append(datos).Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}