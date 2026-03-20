namespace FabricaHilos.LecturaCorreos.Models;

/// <summary>
/// Resultado del intento de parseo de un adjunto XML.
/// </summary>
public enum EstadoParseo
{
    /// <summary>XML UBL reconocido y parseado correctamente.</summary>
    Exito,

    /// <summary>CDR de SUNAT (ApplicationResponse). Se omite intencionalmente.</summary>
    CdrOmitido,

    /// <summary>El contenido no es XML bien formado (BOM, encoding incorrecto, no-XML…).</summary>
    XmlInvalido,

    /// <summary>XML válido pero con un elemento raíz no soportado.</summary>
    TipoNoReconocido,
}

/// <summary>
/// Resultado inmutable del parseo de un adjunto XML UBL.
/// </summary>
public record ResultadoParseo(
    EstadoParseo   Estado,
    DocumentoXml?  Documento   = null,
    string?        Descripcion = null);
