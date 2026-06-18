using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para notificar al equipo de Contabilidad el reporte de documentos
/// Solo SUNAT del RCE (Registro de Compras Electrónico).
/// Corresponde al template: Templates/SireReporteCompras.html
/// </summary>
public class SireReporteComprasPayload : INotificacionPayload
{
    public TipoNotificacion Tipo                => TipoNotificacion.SireReporteCompras;
    public required string  CorreoDestinatario  { get; set; }
    public required string  NombreDestinatario  { get; set; }

    /// <summary>Lista de correos CC separados por coma (puede ser null).</summary>
    public List<string>? CorreosCopia           { get; set; }

    /// <summary>Período en formato AAAAMM, ej: "202606".</summary>
    public required string Periodo              { get; set; }

    public required string CantDocumentos       { get; set; }
    public required string TotalBase            { get; set; }
    public required string TotalIgv             { get; set; }
    public required string TotalImporte         { get; set; }

    /// <summary>Descripción corta de los RUCs excluidos del reporte.</summary>
    public required string ProveedoresExcluidos { get; set; }

    /// <summary>Nombre del usuario o proceso que generó el reporte.</summary>
    public required string GeneradoPor          { get; set; }

    /// <summary>Nombre sugerido para el archivo Excel adjunto.</summary>
    public required string NombreArchivo        { get; set; }

    /// <summary>Bytes del archivo Excel a adjuntar (puede ser null si se omite adjunto).</summary>
    public byte[]? ArchivoExcel                 { get; set; }

    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "Periodo",              Periodo              },
        { "CantDocumentos",       CantDocumentos       },
        { "TotalBase",            TotalBase            },
        { "TotalIgv",             TotalIgv             },
        { "TotalImporte",         TotalImporte         },
        { "ProveedoresExcluidos", ProveedoresExcluidos },
        { "GeneradoPor",          GeneradoPor          },
        { "NombreArchivo",        NombreArchivo        },
    };
}
