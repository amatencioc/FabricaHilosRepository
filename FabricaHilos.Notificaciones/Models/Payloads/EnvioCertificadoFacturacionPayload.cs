using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para notificar al área de facturación sobre un certificado
/// de transacción comercial (TC) listo para procesar.
/// Corresponde al template: Templates/EnvioCertificadoFacturacion.html
/// </summary>
public class EnvioCertificadoFacturacionPayload : INotificacionPayload
{
    // --- Routing del correo ---
    public TipoNotificacion Tipo               => TipoNotificacion.EnvioCertificadoFacturacion;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }

    // --- Datos del certificado ---
    public required string NumeroRequerimiento { get; set; }
    public required string FechaRequerimiento  { get; set; }
    public required string NumeroCertificado   { get; set; }
    public required string TipoCertificado     { get; set; }  // GOTS / OCS
    public required string RazonSocialCliente  { get; set; }
    public required string RucCliente          { get; set; }
    public required string DocumentosAsociados { get; set; }  // Lista de documentos (FV-001-12345, etc.)

    /// <summary>
    /// Mapea las propiedades a los {{placeholders}} del template HTML.
    /// </summary>
    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "NumeroRequerimiento", NumeroRequerimiento },
        { "FechaRequerimiento",  FechaRequerimiento  },
        { "NumeroCertificado",   NumeroCertificado   },
        { "TipoCertificado",     TipoCertificado     },
        { "RazonSocialCliente",  RazonSocialCliente  },
        { "RucCliente",          RucCliente          },
        { "DocumentosAsociados", DocumentosAsociados },
    };
}
