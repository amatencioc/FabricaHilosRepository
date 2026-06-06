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

    /// <summary>5.18 SIRE: exportar propuesta RVIE (genera ticket). Según manual v25 pág 48.</summary>
    public static string RvieExportarPropuesta(string periodo, int codTipoArchivo = 0)
        => $"/libros/rvie/propuesta/web/propuesta/{periodo}/exportapropuesta?codTipoArchivo={codTipoArchivo}";

    /// <summary>5.8 SIRE: aceptar propuesta del RVIE. Según manual v25 pág 34.</summary>
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

    /// <summary>RCE: exportar propuesta RCE (genera ticket). Patrón equivalente a RVIE.</summary>
    public static string RceExportarPropuesta(string periodo, int codTipoArchivo = 0)
        => $"/libros/rce/propuesta/web/propuesta/{periodo}/exportapropuesta?codTipoArchivo={codTipoArchivo}";

    /// <summary>RCE: aceptar propuesta del RCE. Patrón equivalente a RVIE.</summary>
    public static string RceAceptar(string periodo)
        => $"/libros/rce/propuesta/web/propuesta/{periodo}/aceptapropuesta";

    /// <summary>RCE: registrar preliminar RCE. Patrón equivalente a RVIE.</summary>
    public static string RceRegistrarPreliminar(string periodo)
        => $"/libros/rvierce/gestionlibro/web/registroslibros/{periodo}/registrapreliminar";

    /// <summary>RCE: descargar constancia de recepción. Patrón equivalente a RVIE.</summary>
    public static string RceConstancia(string nomArchivo)
        => $"/libros/rvierce/gestionlibro/web/registroslibros/constancia/archivo?nomArchivo={Uri.EscapeDataString(nomArchivo)}";

    // ── Ticket polling y descarga (compartido RVIE/RCE) ─────────────────────

    /// <summary>
    /// 5.16 SIRE: consulta el estado de un ticket asíncrono. Manual v25 pág 43.
    /// perIni/perFin = periodo en formato yyyymm. numTicket = ticket devuelto por la subida TUS.
    /// </summary>
    public static string ConsultarTicket(string ticket, string periodo)
        => $"/libros/rvierce/gestionprocesosmasivos/web/masivo/consultaestadotickets"
         + $"?perIni={periodo}&perFin={periodo}&page=1&perPage=20&numTicket={ticket}";

    /// <summary>
    /// 5.17 SIRE: descarga el archivo generado (ZIP de constancia, propuesta, etc.). Manual v25 pág 46.
    /// nomArchivoReporte y codTipoArchivoReporte vienen del campo archivoReporte del servicio 5.16.
    /// Si codTipoArchivoReporte es null en la respuesta de 5.16, pasar "null" como string.
    /// </summary>
    public static string DescargarArchivo(string nomArchivoReporte, string? codTipoArchivoReporte)
    {
        var cod = string.IsNullOrWhiteSpace(codTipoArchivoReporte) ? "null" : codTipoArchivoReporte;
        return $"/libros/rvierce/gestionprocesosmasivos/web/masivo/archivoreporte"
             + $"?nomArchivoReporte={Uri.EscapeDataString(nomArchivoReporte)}&codTipoArchivoReporte={cod}";
    }
}
