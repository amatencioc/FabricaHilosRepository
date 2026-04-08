using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para notificar a facturación sobre un requerimiento de certificado
/// listo para ser facturado.
/// Corresponde al template: Templates/EnvioCertificadoFacturacion.html
/// </summary>
public class EnvioCertificadoFacturacionPayload : INotificacionPayload
{
    public TipoNotificacion Tipo               => TipoNotificacion.EnvioCertificadoFacturacion;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }
    public string? CorreoCopia { get; set; }

    public required string NumRequerimiento     { get; set; }
    public required string FechaRequerimiento   { get; set; }
    public required string TipoCertificado      { get; set; }
    public required string NumCertificado       { get; set; }
    public required string CodCliente           { get; set; }
    public required string NombreCliente        { get; set; }
    public required string CodVendedor          { get; set; }
    public required string NombreVendedor       { get; set; }
    public required string Moneda               { get; set; }
    public required string Importe              { get; set; }
    public required string TotalFacturas        { get; set; }
    public string? Partidas                     { get; set; }
    public string? OrdenesCompra                { get; set; }

    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "NumRequerimiento",   NumRequerimiento   },
        { "FechaRequerimiento", FechaRequerimiento },
        { "TipoCertificado",    TipoCertificado    },
        { "NumCertificado",     NumCertificado     },
        { "CodCliente",         CodCliente         },
        { "NombreCliente",      NombreCliente      },
        { "CodVendedor",        CodVendedor        },
        { "NombreVendedor",     NombreVendedor     },
        { "Moneda",             Moneda             },
        { "Importe",            Importe            },
        { "TotalFacturas",      TotalFacturas      },
        { "Partidas",           Partidas ?? "No disponible"        },
        { "OrdenesCompra",      OrdenesCompra ?? "No disponible"   },
    };
}
