namespace FabricaHilos.Notificaciones.Models;

/// <summary>
/// Enum de todos los tipos de notificación disponibles.
/// Convención: el nombre del valor = nombre del archivo .html en Templates/
/// Ejemplo: DocumentoLimbo → Templates/DocumentoLimbo.html
/// Para agregar un nuevo tipo: agregar el valor aquí y crear su .html en Templates/
/// </summary>
public enum TipoNotificacion
{
    /// <summary>Notifica al remitente que su PDF no tiene XML válido o tipo no registrado.</summary>
    DocumentoLimbo,

    /// <summary>Notifica a facturación sobre un requerimiento de certificado listo para facturar.</summary>
    EnvioCertificadoFacturacion,

    /// <summary>Notifica al equipo de calidad que un reclamo ha sido enviado para análisis.</summary>
    ReclamoEnviadoCalidad,

    /// <summary>Notifica al vendedor que su reclamo ha sido evaluado y completado.</summary>
    ReclamoEvaluadoVendedor,

    /// <summary>Envía el reporte de documentos Solo SUNAT (SIRE RCE) al equipo de Contabilidad.</summary>
    SireReporteCompras,

    /// <summary>Solicita el visado del jefe responsable para el Alta de un Activo Fijo.</summary>
    VisadoActivoFijoAlta,

    /// <summary>Notifica que la jefatura aceptó el visado del Alta de un Activo Fijo.</summary>
    ConfirmacionVisadoActivoFijoAlta,

    /// <summary>Envía al personal responsable (Mantenimiento/Servicios Generales/Orden y Limpieza)
    /// los hallazgos de una inspección de comedor que le corresponden, con PDF filtrado adjunto.</summary>
    InspeccionHallazgosClasif,

    /// <summary>Envía a RRHH el reporte semanal (jueves) de alertas de tareo: mismo turno
    /// 3 semanas seguidas (TU) o sin descanso 3 semanas seguidas (SD), con Excel adjunto.</summary>
    AlertaTurnoDescansoSemanal,

    // Futuros casos (agregar aquí y crear Templates/{Nombre}.html):
    // DocumentoPorVencer,
    // ErrorProcesamiento,
    // AlertaAdministrativa,
    // ConfirmacionRecepcion,
}
