namespace FabricaHilos.Sire.Constants;

public static class SireEndpoints
{
    public const string ProdAuthBaseUrl = "https://api-seguridad.sunat.gob.pe/v1/clientessol";
    public const string ProdApiBaseUrl  = "https://api-sire.sunat.gob.pe/v1/contribuyente/migeigv";

    /// <summary>
    /// Endpoint TUS para subida de archivos ZIP de propuesta (Reemplazar / Importar).
    /// codProceso: 61=Reemplazar RCE/RVIE, 54=Complementar, 4=Importar comprobantes, 56=No Domiciliados
    /// codLibro: 080000=RCE, 140100=RVIE
    /// </summary>
    public const string TusUploadPath = "/libros/rvierce/receptorpropuesta/web/propuesta/upload";

    // ── RVIE (Registro de Ventas e Ingresos Electrónico) ─────────────────────
    /// <summary>5.33 SIRE: periodos RVIE habilitados para el contribuyente (codLibro=140100).</summary>
    public static string RviePeriodos => "/libros/rvierce/padron/web/omisos/140100/periodos";
    public static string RviePropuesta(string periodo)
        => $"/libros/rvie/propuesta/web/registroslibros/{periodo}/cabecera";
    public static string RvieAceptar(string periodo)
        => $"/libros/rvie/propuesta/web/registroslibros/{periodo}/aceptarpropuesta";
    public static string RvieCierre(string periodo)
        => $"/libros/rvie/propuesta/web/registroslibros/{periodo}/cerrar";
    public static string RvieConstancia(string periodo)
        => $"/libros/rvie/propuesta/web/registroslibros/{periodo}/constancia";

    // ── RCE (Registro de Compras Electrónico) ────────────────────────────────
    /// <summary>5.33 SIRE: periodos RCE habilitados para el contribuyente (codLibro=080000).</summary>
    public static string RcePeriodos => "/libros/rvierce/padron/web/omisos/080000/periodos";
    public static string RcePropuesta(string periodo)
        => $"/libros/rce/propuesta/web/registroslibros/{periodo}/cabecera";
    public static string RceAceptar(string periodo)
        => $"/libros/rce/propuesta/web/registroslibros/{periodo}/aceptarpropuesta";
    public static string RceCierre(string periodo)
        => $"/libros/rce/propuesta/web/registroslibros/{periodo}/cerrar";
    public static string RceConstancia(string periodo)
        => $"/libros/rce/propuesta/web/registroslibros/{periodo}/constancia";

    // ── Ticket polling y descarga (compartido RVIE/RCE) ─────────────────────

    /// <summary>
    /// 5.31 SIRE: consulta el estado de un ticket asíncrono.
    /// perIni/perFin = periodo en formato yyyymm. numTicket = ticket devuelto por la subida TUS.
    /// </summary>
    public static string ConsultarTicket(string ticket, string periodo)
        => $"/libros/rvierce/gestionprocesosmasivos/web/masivo/consultaestadotickets"
         + $"?perIni={periodo}&perFin={periodo}&page=1&perPage=20&numTicket={ticket}";

    /// <summary>
    /// 5.32 SIRE: descarga el archivo generado (ZIP de constancia, propuesta, etc.).
    /// nomArchivoReporte y codTipoArchivoReporte vienen del campo archivoReporte del servicio 5.31.
    /// Si codTipoArchivoReporte es null en la respuesta de 5.31, pasar null.
    /// </summary>
    public static string DescargarArchivo(string nomArchivoReporte, string? codTipoArchivoReporte)
    {
        var cod = string.IsNullOrWhiteSpace(codTipoArchivoReporte) ? "null" : codTipoArchivoReporte;
        return $"/libros/rvierce/gestionprocesosmasivos/web/masivo/archivoreporte"
             + $"?nomArchivoReporte={Uri.EscapeDataString(nomArchivoReporte)}&codTipoArchivoReporte={cod}";
    }
}
