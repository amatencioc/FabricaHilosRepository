namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Registro de rectificación de receta de tintorería (L_RECTIFICA_RECETA).
/// Se genera cuando Control de Calidad rechaza un lote (CTCALIDAD_D.RESULTADO='30')
/// y el laboratorio debe formular una nueva receta antes de reiniciar la tinción.
/// </summary>
public class PlnRectificacionReceta
{
    public long      Numero          { get; set; }
    public DateTime  FchRegistro     { get; set; }

    /// <summary>Área responsable: 'CC'=Control Calidad, 'LA'=Laboratorio.</summary>
    public string?   Area            { get; set; }
    public string?   Situacion       { get; set; }

    /// <summary>'1'=Pendiente, '3'=En Proceso, '6'=Aprobada, '9'=Anulada.</summary>
    public string    Estado          { get; set; } = "1";

    /// <summary>Nombre del laboratorista (JOIN H_TPROD TABLA='09').</summary>
    public string?   Laboratorista   { get; set; }

    /// <summary>Nombre del supervisor (JOIN H_TPROD TABLA='09').</summary>
    public string?   Supervisor      { get; set; }

    public string?   Proceso         { get; set; }

    /// <summary>Código de defecto detectado en CC: '03'=Tono Bajo, '04'=Solidez, '05'=Igualdad…</summary>
    public string?   DefectoOrig     { get; set; }
    public string?   CodCausa        { get; set; }
    public string?   Observacion     { get; set; }

    public string?   MarcaEnproc     { get; set; }
    public DateTime? FchEnProceso    { get; set; }

    public string?   MarcaRectif     { get; set; }
    public DateTime? FchRectificado  { get; set; }

    public string?   MarcaAprob      { get; set; }
    public DateTime? FchAprobado     { get; set; }

    // ── Helpers ──────────────────────────────────────────────────────────────
    public bool   EstaAprobada  => Estado == "6";
    public bool   EstaPendiente => Estado == "1";
    public bool   EstaEnProceso => Estado == "3";
    public bool   EstaActiva    => Estado is "1" or "3";

    public string EstadoLabel => Estado switch
    {
        "1" => "Pendiente",
        "3" => "En Proceso",
        "6" => "Aprobada",
        _   => "Anulada"
    };

    public string EstadoBadge => Estado switch
    {
        "1" => "warning",
        "3" => "info",
        "6" => "success",
        _   => "secondary"
    };
}
