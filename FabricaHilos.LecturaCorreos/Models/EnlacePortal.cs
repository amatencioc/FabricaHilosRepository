namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Representa un enlace de acceso a documentos electrónicos detectado en el cuerpo
/// de un correo que no contiene adjuntos directos (XML/PDF/ZIP).
/// Soporta dos patrones:
///   - Portal JSF (BizLinks): UrlConsultar → página del portal → formulario JSF.
///   - Links directos (efacturacion.pe): UrlXmlDirecto/UrlPdfDirecto → archivo directo.
/// </summary>
public class EnlacePortal
{
    /// <summary>URL de la página del portal JSF (ej. BizLinks consultarDocumento.jsf).</summary>
    public string   UrlConsultar  { get; set; } = string.Empty;

    /// <summary>URL de descarga directa del XML sin portal. Ej: efacturacion.pe descarga.jsf?code=...</summary>
    public string?  UrlXmlDirecto { get; set; }

    /// <summary>URL de descarga directa del PDF sin portal. Ej: efacturacion.pe descarga.jsf?code=...</summary>
    public string?  UrlPdfDirecto { get; set; }

    /// <summary>true si tiene al menos un link directo (XML o PDF) sin necesidad de portal JSF.</summary>
    public bool TieneLinksDirectos => UrlXmlDirecto is not null || UrlPdfDirecto is not null;

    public string   GrupoCorreo  { get; set; } = string.Empty;
    public string   Asunto       { get; set; } = string.Empty;
    public string   Remitente    { get; set; } = string.Empty;
    public DateTime FechaCorreo  { get; set; }
}
