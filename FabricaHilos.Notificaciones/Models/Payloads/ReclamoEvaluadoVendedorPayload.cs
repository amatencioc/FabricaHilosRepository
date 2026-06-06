using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para notificar al vendedor que su reclamo
/// ha sido completamente evaluado y aprobado por gerencia.
/// Corresponde al template: Templates/ReclamoEvaluadoVendedor.html
/// </summary>
public class ReclamoEvaluadoVendedorPayload : INotificacionPayload
{
    // --- Routing del correo ---
    public TipoNotificacion Tipo               => TipoNotificacion.ReclamoEvaluadoVendedor;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }

    // --- Datos del reclamo ---
    public required string IdReclamo          { get; set; }
    public required string NombreCliente      { get; set; }
    public required string RucCliente         { get; set; }
    public required string Asunto             { get; set; }
    public required string FechaCreacion      { get; set; }
    public required string DecisionFinal      { get; set; }
    public required string NombreAnalista     { get; set; }
    public required string NombreGerente      { get; set; }
    public required string FechaAprobacion    { get; set; }
    public required string UrlPortal          { get; set; }

    /// <summary>
    /// Mapea las propiedades a los {{placeholders}} del template HTML.
    /// </summary>
    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "IdReclamo",        IdReclamo        },
        { "NombreCliente",    NombreCliente    },
        { "RucCliente",       RucCliente       },
        { "Asunto",           Asunto           },
        { "FechaCreacion",    FechaCreacion    },
        { "DecisionFinal",    DecisionFinal    },
        { "NombreAnalista",   NombreAnalista   },
        { "NombreGerente",    NombreGerente    },
        { "FechaAprobacion",  FechaAprobacion  },
        { "UrlPortal",        UrlPortal        },
    };
}
