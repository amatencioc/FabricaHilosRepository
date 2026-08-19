namespace FabricaHilos.Alertas.Models;

/// <summary>
/// DTO que refleja una fila de AQUARIUS.V_SCA_ALERTA_TAREO_DETALLE
/// (ver Alertas_Turno_Descanso/V_SCA_ALERTA_TAREO_DETALLE.sql).
/// </summary>
public class AlertaTurnoDescansoDetalle
{
    public long      IdAlerta            { get; set; }
    public string    CodEmpresa          { get; set; } = string.Empty;
    public string    CodPersonal         { get; set; } = string.Empty;
    public string    NombreEmpleado      { get; set; } = string.Empty;
    public string    TipAlerta           { get; set; } = string.Empty;
    public string    TipAlertaDesc       { get; set; } = string.Empty;
    public DateTime  FecIniSemana        { get; set; }
    public DateTime  FecFinSemana        { get; set; }
    public string?   TurnoCod            { get; set; }
    public string?   TurnoDescripcion    { get; set; }
    public string?   HorarioDesc         { get; set; }
    public string?   HoraIngresoTeorica  { get; set; }
    public string?   HoraSalidaTeorica   { get; set; }
    public string?   CodCCostos          { get; set; }
    public string?   CentroCostoNombre   { get; set; }
    public string?   CodArea             { get; set; }
    public string?   AreaNombre          { get; set; }
    public string?   EncargadoNombre     { get; set; }
    public int?      DiasDescanso        { get; set; }
    public string?   Detalle             { get; set; }
    public DateTime  FecDeteccion        { get; set; }
    public string    Estado              { get; set; } = string.Empty;
    public string    Notificado          { get; set; } = string.Empty;
    public DateTime? FecNotificacion     { get; set; }
}
