using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para confirmar que la jefatura ACEPTÓ el visado del alta de un Activo Fijo.
/// Corresponde al template: Templates/ConfirmacionVisadoActivoFijoAlta.html
/// </summary>
public class ConfirmacionVisadoActivoFijoAltaPayload : INotificacionPayload
{
    public TipoNotificacion Tipo               => TipoNotificacion.ConfirmacionVisadoActivoFijoAlta;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }

    public required string CodigoActivo     { get; set; }   // ej: "07-0276"
    public required string Descripcion      { get; set; }
    public required string CCosto           { get; set; }   // ej: "250"
    public required string NombreCC         { get; set; }   // ej: "Sistemas"
    public required string NombreAprobador  { get; set; }   // quien aprobó el visado
    public required string FechaVisado      { get; set; }   // ej: "15/07/2026 15:27"
    public required string UrlFicha         { get; set; }

    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "CodigoActivo",      CodigoActivo      },
        { "Descripcion",       Descripcion       },
        { "CCosto",            CCosto            },
        { "NombreCC",          NombreCC          },
        { "NombreAprobador",   NombreAprobador   },
        { "FechaVisado",       FechaVisado       },
        { "UrlFicha",          UrlFicha          },
        { "NombreDestinatario",NombreDestinatario},
    };
}
