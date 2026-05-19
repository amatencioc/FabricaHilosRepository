namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// DTO de V_PLN_ESTADO_PEDIDO (§8.1 PKG_PLN).
/// Resumen de estado de un pedido completo (todos sus ítems).
/// </summary>
public class PlnEstadoPedido
{
    public int      Serie               { get; set; }
    public long     NumPed              { get; set; }
    public DateTime FchPedido           { get; set; }
    public string   CodCliente          { get; set; } = "";
    public string   NomCliente          { get; set; } = "";
    public string   EstadoPedido        { get; set; } = "";
    public string   Prioridad           { get; set; } = "";
    public int      TotalItems          { get; set; }
    public int      ItemsCerrados       { get; set; }
    public int      ItemsPendientes     { get; set; }
    public int      ItemsConRetraso     { get; set; }
    public decimal  KgTotalPedido       { get; set; }
    public decimal  KgDespachados       { get; set; }
    public decimal  KgPendientes        { get; set; }
    public double   PctAvance           { get; set; }
    public DateTime? FchEntregaMinima   { get; set; }
    public DateTime? FchUltimoDespacho  { get; set; }
    public int      MaxDiasRetraso      { get; set; }
    public DateTime? FchEstDespachoMax  { get; set; }

    // Helpers UI
    public bool EstaRetrasado  => MaxDiasRetraso > 0;
    public bool EstaCerrado    => ItemsPendientes == 0 && TotalItems > 0;
    public string SemaforoCss  => MaxDiasRetraso >= 7 ? "danger"
                                : MaxDiasRetraso >= 3 ? "warning"
                                : MaxDiasRetraso >= 1 ? "info"
                                : "success";
}
