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
        registros = registros
            .Where(r => r.Anulado != "S")
            // Excluir ENTEL SS02/218735290: proveedor dado de baja por SUNAT (error 347)
            .Where(r => !(r.IdPropMatch == null && r.Tipdoc == "14"
                          && r.Serie == "SS02" && r.Numero == "218735290"))
            .ToList();

        // Obtener registros SOLO_SUNAT (CONCIL_ESTADO='3') + EXCLUIDO (CONCIL_ESTADO='5')
        // de SIRE_PROPUESTA para incluirlos en el reemplazo (resolución del error 341).
        var todasPropuesta   = await _repo.GetRegistrosPropuestaAsync(tipo, periodo, ct);
        var propuestaExtra   = todasPropuesta
            .Where(r => r.ConcilEstado == "3" || r.ConcilEstado == "5")
            .OrderBy(r => r.Tipdoc).ThenBy(r => r.Serie).ThenBy(r => r.Numero)
            .ToList();
        _logger.LogInformation(
            "[SIRE-ZIP-LEGACY] Registros propuesta extra (SOLO_SUNAT/EXCLUIDO): {N} tipo={T} periodo={P}",
            propuestaExtra.Count, tipo, periodo);

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

        var txtBytes = GenerarTxtDesdeLegacy(registros, propuestaExtra, esVentas, ruc, razonSocial, periodo);

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
        List<SireLegacyRegistro> registros,
        IReadOnlyList<SireValidaRegistro> propuestaExtra,
        bool esVentas,
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
        // EsNota: nota cualquiera (07/08/87/88) — para campos IMB, etc.
        static bool EsNota(string? tip) =>
            tip == "07" || tip == "08" || tip == "87" || tip == "88";
        // EsNc: SOLO notas de crédito (07/87) — importes negativos.
        // ND (08/88) son débitos y sus importes deben ser POSITIVOS (error 425 si se niegan).
        static bool EsNc(string? tip) => tip is "07" or "87";

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
                var esNota   = EsNota(r.Tipdoc);  // para IMB
                var esNc     = EsNc(r.Tipdoc);    // para negación de importes (solo NC 07/87)

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

                // Valores para notas de crédito (campos 21-24 error 425):
                // NC (07/87): importes NEGATIVOS. ND (08/88): importes POSITIVOS (no negar).
                var valAdqNg  = esNc ? -Math.Abs(r.ValAdqNg)  : r.ValAdqNg;
                var isc       = esNc ? -Math.Abs(r.Isc)        : r.Isc;
                var otros     = esNc ? -Math.Abs(r.Otros)      : r.Otros;

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
                    // F_DOCREF (campo 28): vacío para DUAs (50/52/53/54) y servicios (14) — error 429
                    (r.Tipdoc == "50" || r.Tipdoc == "52" || r.Tipdoc == "53" || r.Tipdoc == "54" || r.Tipdoc == "14")
                        ? string.Empty : D(r.FDocref),  // [o+24] F_DOCREF
                    F(r.TipDocref),         // [o+25] TIP_DOCREF
                    F(r.SerDocref),         // [o+26] SER_DOCREF
                    string.Empty,           // [o+27] COD_DAM      (vacio siempre)
                    NroRef(r.NroDocref),    // [o+28] NRO_DOCREF  (trimmed; obligatorio en 07/08)
                    tipoBien,               // [o+29] TIPO_BIEN
                    string.Empty,           // [o+30] ID_PROYECTO  (vacío; "0" genera error 434)
                    string.Empty,           // [o+31] PORCPART
                    esNota ? "0" : string.Empty, // [o+32] IMB
                    string.Empty,           // [o+33] CAR_MOD
                    string.Empty,           // [o+34] FLAG_DETRAC  (vacío en reemplazo — error 404)
                    string.Empty,           // [o+35] TIPO_NOTA    (no aplica compras)
                    string.Empty,           // [o+36] EST_COMP     (vacio en reemplazo)
                    string.Empty            // [o+37] INCONSIST    (vacio en reemplazo)
                );
            }
            else
            {
                // RVIE: 32 campos de datos
                // Solo NC (07/87) tienen importes negativos; ND (08/88) son positivos.
                var esNcV   = EsNc(r.Tipdoc);
                var iscV    = esNcV ? -Math.Abs(r.Isc)   : r.Isc;
                var otrosV  = esNcV ? -Math.Abs(r.Otros) : r.Otros;
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

        // ── Registros SOLO_SUNAT (estado '3') + EXCLUIDO (estado '5') de SIRE_PROPUESTA ────
        // Se incluyen en el reemplazo con los valores SUNAT para resolver el error 341
        // (registros esperados por SUNAT que no aparecen en el archivo del contribuyente).
        if (!esVentas)
        {
            foreach (var p in propuestaExtra)
            {
                correlativo++;
                var carP     = correlativo.ToString().PadLeft(10, '0');

                // F_VENCTO: fallback para servicios tipo 14 y DUAs
                var fVencP = p.FVencto.HasValue
                    ? p.FVencto.Value.ToString("dd/MM/yyyy")
                    : (p.Tipdoc == "14" || p.Tipdoc == "50" || p.Tipdoc == "52")
                        ? (p.FEmision.HasValue ? p.FEmision.Value.ToString("dd/MM/yyyy") : string.Empty)
                        : string.Empty;

                // ANIO_DAM: obligatorio para DUAs
                var anioDamP = p.AnioDam ?? string.Empty;
                if (string.IsNullOrEmpty(anioDamP) && (p.Tipdoc == "50" || p.Tipdoc == "52") && p.FEmision.HasValue)
                    anioDamP = p.FEmision.Value.Year.ToString();

                var tipoBienP = string.IsNullOrWhiteSpace(p.TipoBien) ? "1" : p.TipoBien!;

                // RUC: vacío → "0" (proveedor extranjero sin RUC) — evita error 401 campo 13
                var rucP    = string.IsNullOrWhiteSpace(p.Ruc) ? "0" : p.Ruc!;
                // NOMBRE: vacío → fallback legible — evita error 422 campo 14
                var nombreP = N100(p.Nombre);
                if (nombreP.Length == 0) nombreP = "SIN NOMBRE";

                // F_DOCREF: vacío para DUAs y servicios tipo 14 (error 429)
                var fDocrefP = (p.Tipdoc == "50" || p.Tipdoc == "52" || p.Tipdoc == "53" || p.Tipdoc == "54" || p.Tipdoc == "14")
                    ? string.Empty
                    : (p.FDocref.HasValue ? p.FDocref.Value.ToString("dd/MM/yyyy") : string.Empty);

                var datosP = string.Join("|",
                    carP,                                                    // [o+0]  CAR_SUNAT
                    p.FEmision.HasValue ? p.FEmision.Value.ToString("dd/MM/yyyy") : string.Empty, // [o+1]
                    fVencP,                                                  // [o+2]  F_VENCTO
                    p.Tipdoc    ?? string.Empty,                             // [o+3]  TIPDOC
                    p.Serie     ?? string.Empty,                             // [o+4]  SERIE
                    anioDamP,                                                // [o+5]  ANIO_DAM
                    (p.Numero   ?? string.Empty).Trim(),                     // [o+6]  NUMERO
                    string.Empty,                                            // [o+7]  NRO_FINAL
                    p.Tdocid    ?? string.Empty,                             // [o+8]  TDOCID
                    rucP,                                                    // [o+9]  RUC (default "0" si vacío)
                    nombreP,                                                 // [o+10] NOMBRE (default "SIN NOMBRE" si vacío)
                    NR(p.BiGravDg),                                          // [o+11] BI_GRAV_DG
                    NR(p.IgvIpmDg),                                          // [o+12] IGV_DG
                    NR(p.BiGravDgng),                                        // [o+13] BI_GRAV_DGNG
                    NR(p.IgvIpmDgng),                                        // [o+14] IGV_DGNG
                    NR(p.BiGravDng),                                         // [o+15] BI_GRAV_DNG
                    NR(p.IgvIpmDng),                                         // [o+16] IGV_DNG
                    NR(p.ValAdqNg),                                          // [o+17] VAL_ADQ_NG
                    NR(p.Isc),                                               // [o+18] ISC
                    NR(p.Icbper),                                            // [o+19] ICBPER
                    NR(p.OtrosTrib),                                         // [o+20] OTROS_TRIB
                    NR(p.TotalCp),                                           // [o+21] TOTAL_CP
                    p.Moneda    ?? string.Empty,                             // [o+22] MONEDA
                    TC(p.Moneda, p.Cambio),                                  // [o+23] TIPO_CAMBIO
                    fDocrefP,                                                // [o+24] F_DOCREF
                    p.TipDocref ?? string.Empty,                             // [o+25] TIP_DOCREF
                    p.SerDocref ?? string.Empty,                             // [o+26] SER_DOCREF
                    string.Empty,                                            // [o+27] COD_DAM
                    (p.NroDocref ?? string.Empty).Trim(),                    // [o+28] NRO_DOCREF
                    tipoBienP,                                               // [o+29] TIPO_BIEN
                    string.Empty,                                            // [o+30] ID_PROYECTO
                    string.Empty,                                            // [o+31] PORCPART
                    string.Empty,                                            // [o+32] IMB
                    string.Empty,                                            // [o+33] CAR_MOD
                    string.Empty,                                            // [o+34] FLAG_DETRAC
                    string.Empty,                                            // [o+35] TIPO_NOTA
                    string.Empty,                                            // [o+36] EST_COMP
                    string.Empty                                             // [o+37] INCONSIST
                );
                sb.Append(prefijo).Append('|').Append(datosP).Append("\r\n");
            }
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}