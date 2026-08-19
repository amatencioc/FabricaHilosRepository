using FabricaHilos.Notificaciones.Abstractions;

namespace FabricaHilos.Notificaciones.Models.Payloads;

/// <summary>
/// Payload para el reporte semanal (jueves) de alertas de tareo: empleados con
/// el mismo turno 3 semanas seguidas (TU) o sin descanso 3 semanas seguidas (SD),
/// generadas por AQUARIUS.PKG_SCA_ALERTAS_TAREO / V_SCA_ALERTA_TAREO_DETALLE.
/// Corresponde al template: Templates/AlertaTurnoDescansoSemanal.html
/// </summary>
public class AlertaTurnoDescansoSemanalPayload : INotificacionPayload
{
    public TipoNotificacion Tipo               => TipoNotificacion.AlertaTurnoDescansoSemanal;
    public required string  CorreoDestinatario { get; set; }
    public required string  NombreDestinatario { get; set; }

    /// <summary>Fecha de corte de la semana evaluada, formateada (dd/MM/yyyy).</summary>
    public required string FechaCorte       { get; set; }

    public required string CantidadAlertas  { get; set; }
    public required string CantidadTurno    { get; set; }
    public required string CantidadDescanso { get; set; }

    /// <summary>Nombre sugerido para el archivo Excel adjunto.</summary>
    public required string NombreArchivo    { get; set; }

    /// <summary>Bytes del archivo Excel a adjuntar (detalle de todas las alertas).</summary>
    public byte[]? ArchivoExcel             { get; set; }

    public Dictionary<string, string> ObtenerReemplazos() => new()
    {
        { "FechaCorte",       FechaCorte       },
        { "CantidadAlertas",  CantidadAlertas  },
        { "CantidadTurno",    CantidadTurno    },
        { "CantidadDescanso", CantidadDescanso },
        { "NombreArchivo",    NombreArchivo    },
    };
}
