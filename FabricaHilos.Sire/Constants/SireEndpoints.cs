namespace FabricaHilos.Sire.Constants;

public static class SireEndpoints
{
    public const string ProdAuthBaseUrl = "https://api-seguridad.sunat.gob.pe/v1/clientessol";
    public const string ProdApiBaseUrl  = "https://api-sire.sunat.gob.pe/v1/contribuyente/migeigv";

    /// <summary>
    /// Endpoint TUS para subida de archivos ZIP de propuesta (Reemplazar / Importar).
    /// codProceso: 61=Reemplazar RCE/RVIE, 54=Complementar, 4=Importar comprobantes, 56=No Domiciliados
    /// codLibro: 080000=RCE, 140000=RVIE (corregido según manual v25)
    /// </summary>
    public const string TusUploadPath = "/libros/rvierce/receptorpropuesta/web/propuesta/upload";

    // ── RVIE (Registro de Ventas e Ingresos Electrónico) ─────────────────────
    /// <summary>5.2 SIRE: periodos RVIE habilitados para el contribuyente (codLibro=140000 según manual v25 pág 24).</summary>
    public static string RviePeriodos => "/libros/rvierce/padron/web/omisos/140000/periodos";

    /// <summary>5.18 SIRE: exportar propuesta RVIE (genera ticket). Según manual v25 pág 48.
    /// Path correcto: /libros/rvie/ (NO rvierce). Sin parámetro codLibro.
    /// </summary>
    public static string RvieExportarPropuesta(string periodo, int codTipoArchivo = 0)
        => $"/libros/rvie/propuesta/web/propuesta/{periodo}/exportapropuesta?codTipoArchivo={codTipoArchivo}";

    /// <summary>5.8 SIRE: aceptar propuesta del RVIE. Según manual v25 pág 34.
    /// Path correcto: /libros/rvie/ (NO rvierce). Sin parámetro codLibro.
    /// </summary>
    public static string RvieAceptar(string periodo)
        => $"/libros/rvie/propuesta/web/propuesta/{periodo}/aceptapropuesta";

    /// <summary>5.9 SIRE: registrar preliminar RVIE. Según manual v25 pág 35.</summary>
    public static string RvieRegistrarPreliminar(string periodo)
        => $"/libros/rvierce/gestionlibro/web/registroslibros/{periodo}/registrapreliminar";

    /// <summary>5.26 SIRE: descargar constancia de recepción. Según manual v25 pág 60.</summary>
    public static string RvieConstancia(string nomArchivo)
        => $"/libros/rvierce/gestionlibro/web/registroslibros/constancia/archivo?nomArchivo={Uri.EscapeDataString(nomArchivo)}";

    // ── RCE (Registro de Compras Electrónico) ────────────────────────────────
    /// <summary>5.2 SIRE: periodos RCE habilitados para el contribuyente (codLibro=080000).</summary>
    public static string RcePeriodos => "/libros/rvierce/padron/web/omisos/080000/periodos";

    /// <summary>5.34 RCE: exportar propuesta RCE (genera ticket). Según manual Compras v22 pág 84.
    /// Path distinto al RVIE: /libros/rce/propuesta/web/propuesta/ con acción exportacioncomprobantepropuesta.
    /// Sin parámetro codLibro.
    /// Parámetros obligatorios: codTipoArchivo (0=txt, 1=csv) y codOrigenEnvio (1=Portal Web).
    /// </summary>
    public static string RceExportarPropuesta(string periodo, int codTipoArchivo = 0, int codOrigenEnvio = 1)
        => $"/libros/rce/propuesta/web/propuesta/{periodo}/exportacioncomprobantepropuesta?codTipoArchivo={codTipoArchivo}&codOrigenEnvio={codOrigenEnvio}";

    /// <summary>5.2 RCE: aceptar propuesta del RCE. Según manual Compras v22 pág 40.
    /// Path distinto al RVIE: /libros/rce/propuesta/web/registroslibros/ con acción aceptarpropuesta.
    /// </summary>
    public static string RceAceptar(string periodo)
        => $"/libros/rce/propuesta/web/registroslibros/{periodo}/aceptarpropuesta";

    /// <summary>5.4 RCE: registrar preliminar RCE. Según manual Compras v22 pág 42.
    /// Path distinto al RVIE: /libros/rce/preliminar/web/registroslibros/ con acción registrapreliminares (plural).
    /// </summary>
    public static string RceRegistrarPreliminar(string periodo)
        => $"/libros/rce/preliminar/web/registroslibros/{periodo}/registrapreliminares";

    /// <summary>5.49 RCE: descargar constancia de recepción. Según manual Compras v22 pág 107.
    /// Path distinto al RVIE: sub-ruta constanciarecepcion con parámetro nomConstanciaRecepcion.
    /// </summary>
    public static string RceConstancia(string nomConstanciaRecepcion)
        => $"/libros/rvierce/gestionlibro/web/registroslibros/constancia/constanciarecepcion?nomConstanciaRecepcion={Uri.EscapeDataString(nomConstanciaRecepcion)}";

    // ── Ticket polling y descarga (compartido RVIE/RCE) ─────────────────────

    /// <summary>
    /// 5.16 SIRE: consulta el estado de un ticket asíncrono. Manual v25 pág 43.
    /// perIni/perFin = periodo en formato yyyymm. numTicket = ticket devuelto por la subida TUS.
    /// </summary>
    public static string ConsultarTicket(string ticket, string periodo)
        => $"/libros/rvierce/gestionprocesosmasivos/web/masivo/consultaestadotickets"
         + $"?perIni={periodo}&perFin={periodo}&page=1&perPage=20&numTicket={ticket}";

    /// <summary>
    /// 5.17 SIRE: descarga el archivo generado (ZIP de propuesta, constancia, etc.). Manual v25 pág 46.
    /// Parámetros obligatorios según el manual:
    /// - nomArchivoReporte: de registros[0].archivoReporte[0].nomArchivoReporte (servicio 5.16)
    /// - codTipoArchivoReporte: de registros[0].archivoReporte[0].codTipoAchivoReporte; si es null, pasar "null"
    /// - codLibro: 140000 para RVIE, 080000 para RCE
    /// - perTributario: de registros[0].perTributario (servicio 5.16)
    /// - codProceso: de registros[0].codProceso (servicio 5.16)
    /// - numTicket: número de ticket
    /// </summary>
    public static string DescargarArchivo(
        string nomArchivoReporte,
        string? codTipoArchivoReporte,
        string codLibro,
        string perTributario,
        string codProceso,
        string numTicket)
    {
        var cod = string.IsNullOrWhiteSpace(codTipoArchivoReporte) ? "null" : codTipoArchivoReporte;
        return $"/libros/rvierce/gestionprocesosmasivos/web/masivo/archivoreporte"
             + $"?nomArchivoReporte={Uri.EscapeDataString(nomArchivoReporte)}"
             + $"&codTipoArchivoReporte={cod}"
             + $"&codLibro={codLibro}"
             + $"&perTributario={perTributario}"
             + $"&codProceso={Uri.EscapeDataString(codProceso)}"
             + $"&numTicket={Uri.EscapeDataString(numTicket)}";
    }
}
