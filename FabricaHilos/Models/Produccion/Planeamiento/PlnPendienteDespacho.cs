namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// DTO de V_PLN_PENDIENTES_DESP (§8.6 PKG_PLN).
/// Ítems listos para despachar (paso '12' o '13', kg_pendientes > 0).
/// </summary>
public class PlnPendienteDespacho
{
    public int      Serie             { get; set; }   // FK de PLN_SEGUIMIENTO
    public long     NumPed            { get; set; }
    public int      Nro               { get; set; }
    public string   CodCliente        { get; set; } = "";
    public string   NomCliente        { get; set; } = "";
    public string   CodArt            { get; set; } = "";
    public string   DescArt           { get; set; } = "";
    public string   Color             { get; set; } = "";
    public string   Titulo            { get; set; } = "";
    public decimal  KgPendientes      { get; set; }
    public decimal  StockDisponible   { get; set; }
    public decimal  KgADespachar      { get; set; }
    public DateTime? FchEntregaComp   { get; set; }
    public int      DiasVencido       { get; set; }   // positivo = vencido
    public int      DiasRetraso       { get; set; }
    public string   IndUrgente        { get; set; } = "";
    public string   CodPasoAct        { get; set; } = "";
    public string   NombrePaso        { get; set; } = "";
    public string   PrioridadPedido   { get; set; } = "";

    // Helpers UI
    public bool EsUrgente     => IndUrgente == "S";
    public bool EstaVencido   => DiasVencido > 0;
    public string SemaforoCss => DiasVencido >= 7 ? "danger"
                               : DiasVencido >= 3 ? "warning"
                               : DiasVencido >= 1 ? "info"
                               : "success";
}
