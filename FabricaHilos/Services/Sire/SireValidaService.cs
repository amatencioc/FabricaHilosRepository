using Oracle.ManagedDataAccess.Client;
using System.IO.Compression;
using System.Text;

namespace FabricaHilos.Services.Sire;

/// <summary>
/// Resultado del proceso de carga de un TXT de propuesta SIRE a Oracle SIRE_PROPUESTA.
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
/// 3. Guardar en SIG.SIRE_PROPUESTA con DELETE del período + INSERT limpio.
///    (SIG.SIRE_VALIDA queda como tabla de respaldo histórico — no se toca aquí.)
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
    /// Carga la propuesta SIRE desde el contenido del ZIP en Oracle SIRE_PROPUESTA.
    /// Borra primero todos los registros del período (DELETE del tipo+período)
    /// y luego inserta los del ZIP → la tabla siempre refleja exactamente lo que SUNAT envió.
    /// </summary>
    /// <param name="contenidoZip">Bytes del archivo ZIP descargado de SUNAT.</param>
    /// <param name="tipoRegistro">"ventas" (TIPO='1') o "compras" (TIPO='2').</param>
    /// <param name="periodo">Período tributario YYYYMM (ej: "202601").</param>
    /// <param name="jobId">JobId del job que originó la descarga (para auditoría).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public async Task<SireValidaCargaResult> CargarDesdeZipAsync(
        byte[] contenidoZip,
        string tipoRegistro,
        string periodo,
        string? jobId = null,
        CancellationToken cancellationToken = default)
    {
        var tipo       = tipoRegistro.Equals("ventas", StringComparison.OrdinalIgnoreCase) ? "1" : "2";
        var periodoNum = int.Parse(periodo);

        var lineas = ExtraerLineasTxt(contenidoZip, tipoRegistro);
        _logger.LogInformation("[SIRE-PROP] ZIP extraído: {Lineas} líneas tipo={Tipo} periodo={Periodo}",
            lineas.Count, tipo, periodo);

        if (lineas.Count == 0)
            return new SireValidaCargaResult { Observaciones = { "El ZIP no contiene líneas de propuesta." } };

        var insertados    = 0;
        var duplicados    = 0;
        var errores       = 0;
        var observaciones = new List<string>();

        const string sqlDelete = @"
DELETE FROM SIG.SIRE_PROPUESTA
WHERE  TIPO    = :tipo
  AND  PERIODO = :periodo";

        const string sqlInsert = @"
INSERT INTO SIG.SIRE_PROPUESTA (
    ID_PROP, TIPO, PERIODO, CAR_SUNAT, JOB_ID,
    F_EMISION, F_VENCTO, CODIGO, TIPDOC, SERIE, NUMERO,
    ANIO_DAM, NROFIN, TDOCID, RUC, NOMBRE,
    BI_GRAV_DG, IGV_IPM_DG, BI_GRAV_DGNG, IGV_IPM_DGNG,
    BI_GRAV_DNG, IGV_IPM_DNG, VAL_ADQ_NG,
    ISC, ICBPER, OTROS_TRIB, TOTAL_CP,
    MONEDA, CAMBIO,
    F_DOCREF, TIP_DOCREF, SER_DOCREF, NRO_DOCREF,
    COD_DAM, TIPO_BIEN, ID_PROYECTO, PORCPART, IMB,
    CAR_MOD, FLAG_DETRAC, TIPO_NOTA,
    EST_COMP, INCONSIST, FCH_CARGA, CONCIL_ESTADO
) VALUES (
    SIG.SEQ_SIRE_PROP.NEXTVAL, :tipo, :periodo, :carSunat, :jobId,
    :fEmision, :fVencto, :codigo, :tipdoc, :serie, :numero,
    :anioDam, :nrofin, :tdocid, :ruc, :nombre,
    :biGravDg, :igvIpmDg, :biGravDgng, :igvIpmDgng,
    :biGravDng, :igvIpmDng, :valAdqNg,
    :isc, :icbper, :otrosTrib, :totalCp,
    :moneda, :cambio,
    :fDocref, :tipDocref, :serDocref, :nroDocref,
    :codDam, :tipoBien, :idProyecto, :porcpart, :imb,
    :carMod, :flagDetrac, :tipoNota,
    :estComp, :inconsist, SYSDATE, '0'
)";

        using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        conn.AutoCommit = true;

        // ── DELETE del período anterior ────────────────────────────────────────
        using (var delCmd = new OracleCommand(sqlDelete, conn))
        {
            delCmd.Parameters.Add(new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipo       });
            delCmd.Parameters.Add(new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodoNum });
            var borrados = await delCmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("[SIRE-PROP] DELETE {B} registros previos tipo={T} periodo={P}",
                borrados, tipo, periodo);
        }

        // ── INSERT de cada línea del TXT ──────────────────────────────────────
        foreach (var linea in lineas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var campos = linea.Split('|');
                // RVIE ventas: 40 cols con prefijo → mínimo 35. RCE compras: 80 cols → mínimo 41.
                var minCols = tipo == "1" ? 35 : 41;
                if (campos.Length < minCols) continue;

                using var cmd = new OracleCommand(sqlInsert, conn);
                cmd.BindByName = true;
                cmd.Parameters.AddRange(ParsearLinea(campos, tipo, periodoNum, jobId));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                insertados++;
            }
            catch (OracleException oex) when (oex.Message.Contains("ORA-00001"))
            {
                duplicados++;
            }
            catch (Exception ex)
            {
                errores++;
                var detalle = $"Error en línea [{linea[..Math.Min(80, linea.Length)]}]: {ex.Message}";
                observaciones.Add(detalle);
                _logger.LogWarning("[SIRE-PROP] {Detalle}", detalle);
            }
        }

        _logger.LogInformation("[SIRE-PROP] Carga completada: {Ins} insertados, {Dup} duplicados, {Err} errores",
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
    /// devuelve un array de OracleParameter para enlazar al OracleCommand con BindByName=true.
    ///
    /// FORMATO SUNAT ACTUAL: 3 campos de cabecera al inicio en ambos tipos:
    ///   [0]=RUC-emisor, [1]=RazonSocial-emisor, [2]=Periodo (YYYYMM numérico).
    /// Se detecta si c[2] es numérico de 6 dígitos. Si no, se asume formato sin prefijo.
    ///
    /// RVIE (ventas, tipo="1") — 40 cols con prefijo (offset o=3):
    ///   [o+0]=CAR_SUNAT, [o+1]=FEmision, [o+2]=FVcto, [o+3]=TipDoc, [o+4]=Serie,
    ///   [o+5]=NumeroInicial, [o+6]=NumeroFinal, [o+7]=TipoDocId, [o+8]=RUC-cliente, [o+9]=Nombre,
    ///   [o+10]=ValorExportacion(skip), [o+11]=BiGravada, [o+12]=DsctoBI(skip), [o+13]=IGV,
    ///   [o+14]=DsctoIGV(skip), [o+15]=MtoExonerado(skip), [o+16]=MtoInafecto(skip), [o+17]=ISC,
    ///   [o+18]=BiGravIVAP(skip), [o+19]=IVAP(skip), [o+20]=ICBPER, [o+21]=OtrosTrib,
    ///   [o+22]=TotalCP, [o+23]=Moneda, [o+24]=TipoCambio, [o+25]=FDocRef, [o+26]=TipDocRef,
    ///   [o+27]=SerDocRef, [o+28]=NroDocRef, [o+29]=IDProyecto, [o+30]=TipoNota, [o+31]=EstComp.
    ///
    /// RCE (compras, tipo="2") — 80 cols con prefijo (offset o=3):
    ///   [o+0]=CAR_SUNAT, [o+1]=FEmision, [o+2]=FVcto, [o+3]=TipDoc, [o+4]=Serie,
    ///   [o+5]=AnioDam, [o+6]=NumeroInicial, [o+7]=NumeroFinal, [o+8]=TipoDocId,
    ///   [o+9]=RUC-proveedor, [o+10]=Nombre, [o+11]=BiGravDG, [o+12]=IgvDG,
    ///   [o+13]=BiGravDGNG, [o+14]=IgvDGNG, [o+15]=BiGravDNG, [o+16]=IgvDNG,
    ///   [o+17]=ValAdqNG, [o+18]=ISC, [o+19]=ICBPER, [o+20]=OtrosTrib, [o+21]=TotalCP,
    ///   [o+22]=Moneda, [o+23]=TipoCambio, [o+24]=FDocRef, [o+25]=TipDocRef,
    ///   [o+26]=SerDocRef, [o+27]=CodDAM, [o+28]=NroDocRef, [o+29]=TipoBien,
    ///   [o+30]=IDProyecto, [o+31]=PorcPart, [o+32]=IMB, [o+33]=CarMod,
    ///   [o+34]=FlagDetrac, [o+35]=TipoNota, [o+36]=EstComp, [o+37]=Inconsist.
    /// </summary>
    private static OracleParameter[] ParsearLinea(string[] c, string tipo, int periodo, string? jobId)
    {
        static DateTime? ParseFecha(string s) =>
            DateTime.TryParseExact(s.Trim(), "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d : null;

        static decimal ParseDec(string s) =>
            decimal.TryParse(s.Trim().Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

        static string Str(string[] arr, int i) =>
            i >= 0 && i < arr.Length ? arr[i].Trim() : string.Empty;

        static OracleParameter Vc(string name, string? value) =>
            new(name, OracleDbType.Varchar2) { Value = (object?)value ?? DBNull.Value };

        static OracleParameter Dt(string name, DateTime? value) =>
            new(name, OracleDbType.Date) { Value = value.HasValue ? (object)value.Value : DBNull.Value };

        static OracleParameter Nm(string name, decimal value) =>
            new(name, OracleDbType.Decimal) { Value = value };

        // Detectar formato con prefijo: c[2] es el período YYYYMM (6 dígitos numéricos).
        int o = c.Length >= 6 && c[2].Trim().Length == 6 && c[2].Trim().All(char.IsDigit) ? 3 : 0;
        bool esVentas = tipo == "1";

        string nombreVal = Str(c, o + (esVentas ? 9 : 10));
        if (nombreVal.Length > 500) nombreVal = nombreVal[..500];

        decimal cambioVal = ParseDec(Str(c, o + (esVentas ? 24 : 23)));

        return
        [
            new OracleParameter("tipo",    OracleDbType.Varchar2) { Value = tipo },
            new OracleParameter("periodo", OracleDbType.Int32)    { Value = periodo },
            Vc("jobId",    jobId),
            Vc("carSunat", Str(c, o + 0)),
            Dt("fEmision", ParseFecha(Str(c, o + 1))),
            Dt("fVencto",  ParseFecha(Str(c, o + 2))),
            Vc("codigo",   Str(c, 0)),            // RUC emisor — siempre índice absoluto 0
            Vc("tipdoc",   Str(c, o + 3)),
            Vc("serie",    Str(c, o + 4)),
            // Ventas: [o+5]=Numero (sin AnioDam). Compras: [o+5]=AnioDam, [o+6]=Numero.
            Vc("numero",   esVentas ? Str(c, o + 5) : Str(c, o + 6)),
            Vc("anioDam",  esVentas ? string.Empty   : Str(c, o + 5)),
            Vc("nrofin",   Str(c, o + (esVentas ? 6 : 7))),
            Vc("tdocid",   Str(c, o + (esVentas ? 7 : 8))),
            Vc("ruc",      Str(c, o + (esVentas ? 8 : 9))),
            Vc("nombre",   nombreVal),
            // Montos — ventas tiene columnas extra intercaladas (DsctoBI, DsctoIGV, IVAP…)
            // Ventas:  [o+11]=BiGravada, [o+12]=DsctoBI(skip), [o+13]=IGV
            // Compras: [o+11]=BiGravDG,  [o+12]=IgvDG,         [o+13]=BiGravDGNG
            Nm("biGravDg",   ParseDec(Str(c, o + 11))),
            Nm("igvIpmDg",   ParseDec(Str(c, o + (esVentas ? 13 : 12)))),
            Nm("biGravDgng", esVentas ? 0m : ParseDec(Str(c, o + 13))),
            Nm("igvIpmDgng", esVentas ? 0m : ParseDec(Str(c, o + 14))),
            Nm("biGravDng",  esVentas ? 0m : ParseDec(Str(c, o + 15))),
            Nm("igvIpmDng",  esVentas ? 0m : ParseDec(Str(c, o + 16))),
            Nm("valAdqNg",   esVentas ? 0m : ParseDec(Str(c, o + 17))),
            // Ventas: ISC=[o+17], ICBPER=[o+20], OtrosTrib=[o+21], TotalCP=[o+22]
            // Compras: ISC=[o+18], ICBPER=[o+19], OtrosTrib=[o+20], TotalCP=[o+21]
            Nm("isc",       ParseDec(Str(c, o + (esVentas ? 17 : 18)))),
            Nm("icbper",    ParseDec(Str(c, o + (esVentas ? 20 : 19)))),
            Nm("otrosTrib", ParseDec(Str(c, o + (esVentas ? 21 : 20)))),
            Nm("totalCp",   ParseDec(Str(c, o + (esVentas ? 22 : 21)))),
            Vc("moneda",    string.IsNullOrWhiteSpace(Str(c, o + (esVentas ? 23 : 22))) ? "PEN" : Str(c, o + (esVentas ? 23 : 22))),
            Nm("cambio",    cambioVal == 0m ? 1m : cambioVal),
            // Documentos de referencia — ventas desplazado +1 respecto a compras salvo NroDocRef
            // Ventas: FDocRef=[o+25], TipDocRef=[o+26], SerDocRef=[o+27], NroDocRef=[o+28]
            // Compras: FDocRef=[o+24], TipDocRef=[o+25], SerDocRef=[o+26], CodDAM=[o+27], NroDocRef=[o+28]
            Dt("fDocref",   ParseFecha(Str(c, o + (esVentas ? 25 : 24)))),
            Vc("tipDocref", Str(c, o + (esVentas ? 26 : 25))),
            Vc("serDocref", Str(c, o + (esVentas ? 27 : 26))),
            Vc("nroDocref", Str(c, o + 28)),              // [o+28] en ambos formatos
            Vc("codDam",    esVentas ? string.Empty : Str(c, o + 27)),
            Vc("tipoBien",  esVentas ? string.Empty : Str(c, o + 29)),
            // IDProyecto: ventas=[o+29], compras=[o+30]
            Vc("idProyecto", Str(c, o + (esVentas ? 29 : 30))),
            Nm("porcpart",   esVentas ? 0m : ParseDec(Str(c, o + 31))),
            Nm("imb",        esVentas ? 0m : ParseDec(Str(c, o + 32))),
            Vc("carMod",     esVentas ? string.Empty : Str(c, o + 33)),
            Vc("flagDetrac", esVentas ? string.Empty : Str(c, o + 34)),
            // TipoNota: ventas=[o+30], compras=[o+35]. EstComp: ventas=[o+31], compras=[o+36].
            Vc("tipoNota",  Str(c, o + (esVentas ? 30 : 35))),
            Vc("estComp",   string.IsNullOrWhiteSpace(Str(c, o + (esVentas ? 31 : 36))) ? "1" : Str(c, o + (esVentas ? 31 : 36))),
            Vc("inconsist", esVentas ? "0" : (string.IsNullOrWhiteSpace(Str(c, o + 37)) ? "0" : Str(c, o + 37))),
        ];
    }
}
