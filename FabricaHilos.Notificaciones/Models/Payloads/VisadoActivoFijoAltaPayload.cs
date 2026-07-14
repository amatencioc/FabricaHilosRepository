using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para solicitar el visado del responsable (jefe del C.Costo)
/// sobre el alta de un Activo Fijo.
/// Corresponde al template: Templates/VisadoActivoFijoAlta.html
/// </summary>
public class VisadoActivoFijoAltaPayload : INotificacionPayload
{
    // ── Routing ────────────────────────────────────────────────────────────
    public TipoNotificacion Tipo               => TipoNotificacion.VisadoActivoFijoAlta;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }

    // ── Datos del activo ────────────────────────────────────────────────────
    public required string CodigoActivo     { get; set; }   // ej: "03-0643"
    public required string ClaseActivo      { get; set; }   // ej: "Maquinarias y Accesorios"
    public required string Descripcion      { get; set; }
    public required string CCosto           { get; set; }   // ej: "P560"
    public required string NombreCC         { get; set; }   // ej: "Tintorería General"
    public required string ValorAdquisicion { get; set; }   // ej: "S/ 1,188,151.97"
    public required string FechaRecepcion   { get; set; }   // ej: "25/07/2025"
    public required string NombreRegistrador{ get; set; }   // quien registró el alta
    public required string FechaRegistro    { get; set; }   // ej: "14/07/2026"
    public string?  ObsAlta                 { get; set; }   // observaciones del alta
    public string?  FechaOperacion          { get; set; }   // F_OPERA si ya se fijó

    // ── Links de acción (tokenizados) ───────────────────────────────────────
    public required string UrlAprobar  { get; set; }
    public required string UrlObservar { get; set; }
    public required string UrlFicha    { get; set; }
    public required string FechaExpira { get; set; }   // ej: "14/08/2026"

    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "CodigoActivo",      CodigoActivo      },
        { "ClaseActivo",       ClaseActivo       },
        { "Descripcion",       Descripcion       },
        { "CCosto",            CCosto            },
        { "NombreCC",          NombreCC          },
        { "ValorAdquisicion",  ValorAdquisicion  },
        { "FechaRecepcion",    FechaRecepcion    },
        { "NombreRegistrador", NombreRegistrador },
        { "FechaRegistro",     FechaRegistro     },
        { "ObsAlta",           ObsAlta ?? "—"   },
        { "FechaOperacion",    FechaOperacion ?? "—" },
        { "UrlAprobar",        UrlAprobar        },
        { "UrlObservar",       UrlObservar       },
        { "UrlFicha",          UrlFicha          },
        { "FechaExpira",       FechaExpira       },
        { "NombreDestinatario",NombreDestinatario},
    };
}
