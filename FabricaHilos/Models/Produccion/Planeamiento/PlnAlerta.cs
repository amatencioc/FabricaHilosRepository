namespace FabricaHilos.Models.Produccion.Planeamiento;

public class PlnAlerta
{
    public long     IdAlerta   { get; set; }
    public long?    IdSeguim   { get; set; }
    public string   TipAlerta  { get; set; } = "";
    public string   Nivel      { get; set; } = "B";
    public string   Titulo     { get; set; } = "";
    public string?  Detalle    { get; set; }
    public DateTime FchAlerta  { get; set; }
    public DateTime? FchLimite { get; set; }
    public int?     DiasRetraso { get; set; }
    public string?  CodMaq     { get; set; }
    public string   Estado     { get; set; } = "A";

    // Datos desnormalizados del seguimiento
    public long?    NumPed        { get; set; }
    public int?     Serie         { get; set; }
    public int?     Nro           { get; set; }  // BUG FIX: V_PLN_ALERTAS_ACTIVAS.nro no se mapeaba
    public int?     NumDet        { get; set; }  // sublote (0 = único); desde V_PLN_ALERTAS_ACTIVAS.num_det (v2.4)
    public string?  CodArt        { get; set; }
    public string?  CodCliente    { get; set; }
    public string?  NombreCliente { get; set; }
    public string?  CodPasoAct    { get; set; }
    public string?  ColorUiPaso   { get; set; }

    // BUG FIX: V_PLN_ALERTAS_ACTIVAS.horas_sin_resolver (la vista devuelve ROUND(*24,2) = horas reales)
    public double?  HorasSinResolver { get; set; }

    // Campos enriquecidos v2.3 — JOIN con PLN_SEGUIMIENTO + PLN_ESTADO_CODIGO en la vista
    public string?  TituloArt      { get; set; }
    public string?  Proceso        { get; set; }  // '01'=Cardado '20'=Peinado '24'=P.Gaseado
    public string?  NombrePaso     { get; set; }  // nombre del paso actual
    public DateTime? FchEntregaComp { get; set; } // fecha compromiso con cliente
    public int?     DiasRetrasoEnt { get; set; }  // TRUNC(SYSDATE)-TRUNC(FCH_ENTREGA_COMP)
    public decimal? CantidadOrig   { get; set; }  // kg pedidos original
    public decimal? KgPendientes   { get; set; }  // kg aún sin despachar
    public int?     NroCiclo       { get; set; }  // 1=normal, 2+=reproceso
    public string?  IndUrgente     { get; set; }  // 'S' si urgente

    // Campos de resolución (solo presentes en historial: ESTADO='R'/'I')
    public DateTime? FchResolucion  { get; set; }
    public string?   UsuarioResuelve { get; set; }

    // Alias para la vista
    public DateTime FchGeneracion => FchAlerta;

    public string NivelColor => Nivel switch
    {
        "C" => "danger",
        "A" => "warning",
        "M" => "info",
        "B" => "secondary",
        _   => "secondary"
    };

    public string NivelTexto => Nivel switch
    {
        "C" => "Crítico",
        "A" => "Alto",
        "M" => "Medio",
        "B" => "Bajo",
        _   => "Bajo"
    };

    public string TipoTexto => TipAlerta switch
    {
        "RET1" => "Retraso crítico",
        "RET2" => "Retraso alto",
        "SMP"  => "Sin planificación",
        "STN"  => "Sin ingresar a TT",
        "QCF"  => "CC rechazado",
        _      => TipAlerta
    };

    public string ProcesoTexto => Proceso switch
    {
        "01" => "Cardado",
        "20" => "Peinado",
        "24" => "P.Gaseado",
        _    => Proceso ?? ""
    };

    /// Porcentaje de KG pendientes sobre el total pedido (0-100), o null si no hay datos.
    public double? KgPorcentajePendiente =>
        CantidadOrig > 0 && KgPendientes.HasValue
            ? Math.Round((double)KgPendientes.Value / (double)CantidadOrig.Value * 100, 1)
            : (double?)null;
}
