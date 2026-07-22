using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para notificar al personal responsable (Mantenimiento / Servicios Generales /
/// Orden y Limpieza) los hallazgos de una inspección de comedor que le corresponden,
/// adjuntando un PDF filtrado SOLO con esos hallazgos (ver
/// InspeccionComController.EnviarCorreoClasif y SoInspeccionPdfService.GenerarPorClasificacion).
/// Corresponde al template: Templates/InspeccionHallazgosClasif.html
/// </summary>
public class InspeccionHallazgosClasifPayload : INotificacionPayload
{
    public TipoNotificacion Tipo               => TipoNotificacion.InspeccionHallazgosClasif;
    public required string  CorreoDestinatario  { get; set; }
    public required string  NombreDestinatario  { get; set; }

    /// <summary>Correos adicionales del resto del personal asignado a la clasificación (To).</summary>
    public List<string>? CorreosTo             { get; set; }

    public required string NombreComedor       { get; set; }
    public required string FechaInsp           { get; set; }
    public required string Clasificacion       { get; set; }
    public required string CantHallazgos       { get; set; }

    /// <summary>Nombre sugerido para el archivo PDF adjunto.</summary>
    public required string NombreArchivo       { get; set; }

    /// <summary>Bytes del PDF filtrado por clasificación a adjuntar.</summary>
    public byte[]? ArchivoPdf                  { get; set; }

    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "NombreComedor", NombreComedor },
        { "FechaInsp",     FechaInsp     },
        { "Clasificacion", Clasificacion },
        { "CantHallazgos", CantHallazgos },
        { "NombreArchivo", NombreArchivo },
    };
}
