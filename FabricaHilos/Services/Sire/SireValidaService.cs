using Oracle.ManagedDataAccess.Client;
using System.IO.Compression;
using System.Text;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Resultado del proceso de carga de un TXT de propuesta SIRE a Oracle SIRE_VALIDA.
/// </summary>
public sealed class SireValidaCargaResult
{
    public int Insertados   { get; init; }
    public int Duplicados   { get; init; }
    public int Errores      { get; init; }
    public List<string> Observaciones { get; init; } = new();
}

/// <summary>
/// Servicio encargado de:
/// 1. Extraer el archivo TXT de propuesta del ZIP de SUNAT.
/// 2. Parsear cada línea (formato pipe-delimited según estándar SIRE).
/// 3. Hacer UPSERT (MERGE) en Oracle SIG.SIRE_VALIDA con ORIGEN='P'.
/// </summary>
public sealed class SireValidaService
{
    private readonly string _connectionString;
    private readonly ILogger<SireValidaService> _logger;

    public SireValidaService(IConfiguration configuration, ILogger<SireValidaService> logger)
    {
        _connectionString = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("LaColonialConnection no encontrada en configuración.");
        _logger = logger;
    }

    /// <summary>
    /// Carga la propuesta SIRE desde el contenido del ZIP en Oracle SIRE_VALIDA.
    /// </summary>
    /// <param name="contenidoZip">Bytes del archivo ZIP descargado de SUNAT.</param>
    /// <param name="tipoRegistro">"ventas" (TIPO='1') o "compras" (TIPO='2').</param>
    /// <param name="periodo">Período tributario YYYYMM (ej: "202601").</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public async Task<SireValidaCargaResult> CargarDesdeZipAsync(
        byte[] contenidoZip,
        string tipoRegistro,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        var tipo = tipoRegistro.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";
        var periodoNum = int.Parse(periodo);

        var lineas = ExtraerLineasTxt(contenidoZip, tipoRegistro);
        _logger.LogInformation("[SIRE-VALIDA] ZIP extraído: {Lineas} líneas para tipo={Tipo} periodo={Periodo}",
            lineas.Count, tipo, periodo);

        if (lineas.Count == 0)
            return new SireValidaCargaResult { Observaciones = { "El ZIP no contiene líneas de propuesta." } };

        var insertados  = 0;
        var duplicados  = 0;
        var errores     = 0;
        var observaciones = new List<string>();

        const string sql = @"
MERGE INTO SIG.SIRE_VALIDA t
USING (SELECT :tipo TIPO, :periodo PERIODO, :carSunat CAR_SUNAT FROM DUAL) s
ON (t.TIPO = s.TIPO AND t.PERIODO = s.PERIODO AND t.CAR_SUNAT = s.CAR_SUNAT)
WHEN MATCHED THEN UPDATE SET
    t.F_EMISION    = :fEmision,
    t.F_VENCTO     = :fVencto,
    t.TIPDOC       = :tipdoc,
    t.SERIE        = :serie,
    t.NUMERO       = :numero,
    t.RUC          = :ruc,
    t.NOMBRE       = :nombre,
    t.BI_GRAV_DG   = :biGravDg,
    t.IGV_IPM_DG   = :igvIpmDg,
    t.BI_GRAV_DGNG = :biGravDgng,
    t.IGV_IPM_DGNG = :igvIpmDgng,
    t.BI_GRAV_DNG  = :biGravDng,
    t.IGV_IPM_DNG  = :igvIpmDng,
    t.VAL_ADQ_NG   = :valAdqNg,
    t.ISC          = :isc,
    t.ICBPER       = :icbper,
    t.OTROS_TRIB   = :otrosTrib,
    t.TOTAL_CP     = :totalCp,
    t.MONEDA       = :moneda,
    t.CAMBIO       = :cambio,
    t.EST_COMP     = :estComp,
    t.INCONSIST    = :inconsist
WHEN NOT MATCHED THEN INSERT (
    TIPO, PERIODO, ORIGEN, CAR_SUNAT,
    F_EMISION, F_VENCTO, CODIGO, TIPDOC, SERIE, NUMERO,
    ANIO_DAM, NROFIN, TDOCID, RUC, NOMBRE,
    BI_GRAV_DG, IGV_IPM_DG, BI_GRAV_DGNG, IGV_IPM_DGNG,
    BI_GRAV_DNG, IGV_IPM_DNG, VAL_ADQ_NG,
    ISC, ICBPER, OTROS_TRIB, TOTAL_CP,
    MONEDA, CAMBIO,
    F_DOCREF, TIP_DOCREF, SER_DOCREF, NRO_DOCREF,
    COD_DAM, TIPO_BIEN, ID_PROYECTO, PORCPART, IMB,
    CAR_MOD, FLAG_DETRAC, TIPO_NOTA,
    EST_COMP, INCONSIST, EST_LOGIX, ESTADO, OBSERVACION
) VALUES (
    :tipo, :periodo, 'P', :carSunat,
    :fEmision, :fVencto, :codigo, :tipdoc, :serie, :numero,
    :anioDam, :nrofin, :tdocid, :ruc, :nombre,
    :biGravDg, :igvIpmDg, :biGravDgng, :igvIpmDgng,
    :biGravDng, :igvIpmDng, :valAdqNg,
    :isc, :icbper, :otrosTrib, :totalCp,
    :moneda, :cambio,
    :fDocref, :tipDocref, :serDocref, :nroDocref,
    :codDam, :tipoBien, :idProyecto, :porcpart, :imb,
    :carMod, :flagDetrac, :tipoNota,
    :estComp, :inconsist, NULL, '0', NULL
)";

        using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        foreach (var linea in lineas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var campos = linea.Split('|');
                if (campos.Length < 20) continue;

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.AddRange(ParsearLinea(campos, tipo, periodoNum));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                insertados++;
            }
            catch (OracleException oex) when (oex.Message.Contains("ORA-00001"))
            {
                // Clave única duplicada (no debería pasar con MERGE, pero por seguridad)
                duplicados++;
            }
            catch (Exception ex)
            {
                errores++;
                var detalle = $"Error en línea [{linea[..Math.Min(80, linea.Length)]}]: {ex.Message}";
                observaciones.Add(detalle);
                _logger.LogWarning("[SIRE-VALIDA] {Detalle}", detalle);
            }
        }

        _logger.LogInformation("[SIRE-VALIDA] Carga completada: {Ins} insertados/actualizados, {Dup} duplicados, {Err} errores",
            insertados, duplicados, errores);

        return new SireValidaCargaResult
        {
            Insertados    = insertados,
            Duplicados    = duplicados,
            Errores       = errores,
            Observaciones = observaciones
        };
    }

    /// <summary>
    /// Extrae las líneas de datos del TXT de propuesta contenido dentro del ZIP.
    /// Busca el archivo TXT más grande (el que tiene los comprobantes), omite la cabecera.
    /// </summary>
    private static List<string> ExtraerLineasTxt(byte[] contenidoZip, string tipoRegistro)
    {
        var lineas = new List<string>();

        using var ms     = new MemoryStream(contenidoZip);
        using var zip    = new ZipArchive(ms, ZipArchiveMode.Read);

        // El ZIP de SUNAT puede tener múltiples entradas; tomamos el TXT de mayor tamaño
        var entradaTxt = zip.Entries
            .Where(e => e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                     || e.Name.EndsWith(".TXT", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Length)
            .FirstOrDefault();

        if (entradaTxt is null) return lineas;

        using var reader = new StreamReader(entradaTxt.Open(), Encoding.Latin1);

        var primera = true;
        while (reader.ReadLine() is string linea)
        {
            // Omitir línea de cabecera si existe (empieza con letras, no con dígitos ni pipe)
            if (primera)
            {
                primera = false;
                // Si la primera línea tiene cabecera textual, la saltamos
                if (!string.IsNullOrWhiteSpace(linea) && !char.IsDigit(linea[0]) && linea[0] != '|')
                    continue;
            }

            if (!string.IsNullOrWhiteSpace(linea))
                lineas.Add(linea);
        }

        return lineas;
    }

    /// <summary>
    /// Parsea los campos pipe-delimited de una línea del TXT de propuesta SIRE y
    /// devuelve un array de OracleParameter listo para enlazar al OracleCommand.
    /// Formato según Manual SIRE SUNAT (compras v22 / ventas v25).
    /// Posiciones: [0]=CAR_SUNAT, [1]=F_EMISION, [2]=F_VENCTO, [3]=CODIGO(RUC emisor),
    ///   [4]=TIPDOC, [5]=SERIE, [6]=NUMERO, [7]=ANIO_DAM, [8]=NROFIN,
    ///   [9]=TDOCID, [10]=RUC, [11]=NOMBRE, [12]=BI_GRAV_DG, [13]=IGV_IPM_DG,
    ///   [14]=BI_GRAV_DGNG, [15]=IGV_IPM_DGNG, [16]=BI_GRAV_DNG, [17]=IGV_IPM_DNG,
    ///   [18]=VAL_ADQ_NG, [19]=ISC, [20]=ICBPER, [21]=OTROS_TRIB, [22]=TOTAL_CP,
    ///   [23]=MONEDA, [24]=CAMBIO, [25]=F_DOCREF, [26]=TIP_DOCREF, [27]=SER_DOCREF,
    ///   [28]=NRO_DOCREF, [29]=COD_DAM, [30]=TIPO_BIEN, [31]=ID_PROYECTO,
    ///   [32]=PORCPART, [33]=IMB, [34]=CAR_MOD, [35]=FLAG_DETRAC, [36]=TIPO_NOTA,
    ///   [37]=EST_COMP, [38]=INCONSIST.
    /// </summary>
    private static OracleParameter[] ParsearLinea(string[] c, string tipo, int periodo)
    {
        static DateTime? ParseFecha(string s) =>
            DateTime.TryParseExact(s.Trim(), "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d : null;

        static decimal ParseDec(string s) =>
            decimal.TryParse(s.Trim().Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

        static int? ParseInt(string s) =>
            int.TryParse(s.Trim(), out var i) ? i : null;

        static string Str(string[] arr, int i) =>
            i < arr.Length ? arr[i].Trim() : string.Empty;

        static OracleParameter Vc(string name, string? value) =>
            new(name, OracleDbType.Varchar2) { Value = (object?)value ?? DBNull.Value };

        static OracleParameter Dt(string name, DateTime? value) =>
            new(name, OracleDbType.Date) { Value = value.HasValue ? (object)value.Value : DBNull.Value };

        static OracleParameter Nm(string name, decimal value) =>
            new(name, OracleDbType.Decimal) { Value = value };

        static OracleParameter NmNullable(string name, int? value) =>
            new(name, OracleDbType.Int32) { Value = value.HasValue ? (object)value.Value : DBNull.Value };

        var nombreVal = Str(c, 11);
        if (nombreVal.Length > 500) nombreVal = nombreVal[..500];

        var cambioVal = ParseDec(Str(c, 24));

        return
        [
            new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipo },
            new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo },
            Vc("carSunat",   Str(c, 0)),
            Dt("fEmision",   ParseFecha(Str(c, 1))),
            Dt("fVencto",    ParseFecha(Str(c, 2))),
            Vc("codigo",     Str(c, 3)),
            Vc("tipdoc",     Str(c, 4)),
            Vc("serie",      Str(c, 5)),
            Vc("numero",     Str(c, 6)),
            NmNullable("anioDam",   ParseInt(Str(c, 7))),
            Vc("nrofin",     Str(c, 8)),
            Vc("tdocid",     Str(c, 9)),
            Vc("ruc",        Str(c, 10)),
            Vc("nombre",     nombreVal),
            Nm("biGravDg",   ParseDec(Str(c, 12))),
            Nm("igvIpmDg",   ParseDec(Str(c, 13))),
            Nm("biGravDgng", ParseDec(Str(c, 14))),
            Nm("igvIpmDgng", ParseDec(Str(c, 15))),
            Nm("biGravDng",  ParseDec(Str(c, 16))),
            Nm("igvIpmDng",  ParseDec(Str(c, 17))),
            Nm("valAdqNg",   ParseDec(Str(c, 18))),
            Nm("isc",        ParseDec(Str(c, 19))),
            Nm("icbper",     ParseDec(Str(c, 20))),
            Nm("otrosTrib",  ParseDec(Str(c, 21))),
            Nm("totalCp",    ParseDec(Str(c, 22))),
            Vc("moneda",     string.IsNullOrWhiteSpace(Str(c, 23)) ? "PEN" : Str(c, 23)),
            Nm("cambio",     cambioVal == 0m ? 1m : cambioVal),
            Dt("fDocref",    ParseFecha(Str(c, 25))),
            Vc("tipDocref",  Str(c, 26)),
            Vc("serDocref",  Str(c, 27)),
            Vc("nroDocref",  Str(c, 28)),
            Vc("codDam",     Str(c, 29)),
            Vc("tipoBien",   Str(c, 30)),
            Vc("idProyecto", Str(c, 31)),
            Nm("porcpart",   ParseDec(Str(c, 32))),
            Nm("imb",        ParseDec(Str(c, 33))),
            Vc("carMod",     Str(c, 34)),
            Vc("flagDetrac", Str(c, 35)),
            Vc("tipoNota",   Str(c, 36)),
            Vc("estComp",    string.IsNullOrWhiteSpace(Str(c, 37)) ? "1" : Str(c, 37)),
            Vc("inconsist",  string.IsNullOrWhiteSpace(Str(c, 38)) ? "0" : Str(c, 38)),
        ];
    }
}
