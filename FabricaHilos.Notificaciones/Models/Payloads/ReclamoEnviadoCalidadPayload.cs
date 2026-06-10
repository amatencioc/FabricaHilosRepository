using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para notificar al equipo de calidad que un reclamo
/// ha sido enviado desde ventas para su análisis.
/// Corresponde al template: Templates/ReclamoEnviadoCalidad.html
/// </summary>
public class ReclamoEnviadoCalidadPayload : INotificacionPayload
{
    // --- Routing del correo ---
    public TipoNotificacion Tipo               => TipoNotificacion.ReclamoEnviadoCalidad;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }

    /// <summary>
    /// Dirección de correo en copia (CC). Se usa para copiar al vendedor que envía el reclamo.
    /// </summary>
    public string? CorreoCopia { get; set; }

    // --- Datos del reclamo ---
    public required string IdReclamo          { get; set; }
    public required string NombreCliente      { get; set; }
    public required string RucCliente         { get; set; }
    public required string Asunto             { get; set; }
    public required string NombreVendedor     { get; set; }
    public required string CorreoVendedor     { get; set; }
    public required string FechaCreacion      { get; set; }
    public required string Descripcion        { get; set; }

    /// <summary>
    /// Mapea las propiedades a los {{placeholders}} del template HTML.
    /// </summary>
    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "IdReclamo",       IdReclamo       },
        { "NombreCliente",   NombreCliente   },
        { "RucCliente",      RucCliente      },
        { "Asunto",          Asunto          },
        { "NombreVendedor",  NombreVendedor  },
        { "CorreoVendedor",  CorreoVendedor  },
        { "FechaCreacion",   FechaCreacion   },
        { "Descripcion",     Descripcion     },
    };
}
