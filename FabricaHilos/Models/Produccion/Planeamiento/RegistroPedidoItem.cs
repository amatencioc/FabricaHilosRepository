namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Fila plana de un ítem de pedido con todos los campos relevantes:
/// cabecera de pedido + línea ITEMPED + artículo + familia/línea textil.
/// Se usa en la vista Registro de Pedidos (Index).
/// </summary>
public class RegistroPedidoItem
{
    // ── Pedido ────────────────────────────────────────────────────────────
    public int      Serie       { get; set; }
    public long     NumPed      { get; set; }
    public DateTime FchPedido   { get; set; }
    public DateTime? FchAprobacion { get; set; }
    public string   CodCliente  { get; set; } = "";
    public string   NombreCliente { get; set; } = "";
    public string   CodVende    { get; set; } = "";
    public string   Giro        { get; set; } = "";
    public string   EstadoPed   { get; set; } = "";   // 0=sin aprobar 5=aprobado 9=cerrado

    // ── Ítem (ITEMPED) ────────────────────────────────────────────────────
    public int      Nro         { get; set; }
    public string   CodArt      { get; set; } = "";
    public string   DescArt     { get; set; } = "";
    public string   Titulo      { get; set; } = "";
    public string   TipoFibra   { get; set; } = "";
    public string   Proceso     { get; set; } = "";
    public string   CodServ     { get; set; } = "";
    public decimal  Cantidad    { get; set; }
    public decimal  Precio      { get; set; }
    public string   Color       { get; set; } = "";
    public string   ColorDet    { get; set; } = "";   // descripción libre del color
    public string   Intensidad  { get; set; } = "";
    public string   Presentacion { get; set; } = "";
    public string   EstadoItem  { get; set; } = "";
    public DateTime? FMaxPed    { get; set; }
    public string   SoloDespacho { get; set; } = "N";
    public string   Detalle     { get; set; } = "";

    // ── Artículo → Familia / Línea ────────────────────────────────────────
    public string   CodFam      { get; set; } = "";
    public string   CodLin      { get; set; } = "";
    public string   DescFamilia { get; set; } = "";
    public string   DescLinea   { get; set; } = "";

    // ── Computed helpers ──────────────────────────────────────────────────

    /// Área lógica derivada de COD_SERV
    public string AreaServicio => CodServ switch
    {
        "C"   => "Hilandería",
        "CT"  => "Hilandería + Tintorería",
        "ST"  => "Solo Tintorería",
        "STD" => "Tintorería Directa",
        "STR" => "Tintorería + Retorcido",
        "SR"  => "Retorcido",
        "SRT" => "Retorcido + Tintorería",
        "SE"  => "Servicio Especial",
        "M"   => "Moulinex",
        _     when CodServ.StartsWith("S") => "Servicio " + CodServ,
        _     => CodServ
    };

    /// Nombre legible del proceso productivo
    public string NombreProceso => Proceso switch
    {
        "01" => "Cardado",
        "20" => "Peinado",
        "24" => "Peinado Gaseado",
        _    => Proceso
    };

    /// Bootstrap color class para el badge de área
    public string AreaBadgeClass => CodServ switch
    {
        "C"   => "bg-info text-dark",
        "CT"  => "bg-primary",
        "ST"  => "bg-purple text-white",
        _     when CodServ.StartsWith("S") => "bg-secondary",
        _     => "bg-dark"
    };

    /// Estado del pedido en texto
    public string EstadoPedTexto => EstadoPed switch
    {
        "0" => "Sin Aprobar",
        "5" => "Aprobado",
        "9" => "Cerrado",
        _   => EstadoPed
    };

    /// Indica si el item ya tiene fecha de entrega y está vencido
    public bool EsVencido => FMaxPed.HasValue && FMaxPed.Value < DateTime.Today;
}

/// <summary>
/// ViewModel para la vista Registro de Pedidos.
/// </summary>
public class RegistroPedidosViewModel
{
    public IReadOnlyList<RegistroPedidoItem> Items { get; set; } = [];

    // ── Filtros activos ───────────────────────────────────────────────────
    public DateTime FchDesde    { get; set; }
    public DateTime FchHasta    { get; set; }
    public string   FiltroServ  { get; set; } = "";
    public string   FiltroCliente { get; set; } = "";
    public string   FiltroProceso { get; set; } = "";
    public string   FiltroEstado  { get; set; } = "";

    // ── KPIs calculados ───────────────────────────────────────────────────
    public int     TotalPedidos    => Items.Select(x => x.NumPed).Distinct().Count();
    public int     TotalItems      => Items.Count;
    public decimal TotalKg         => Items.Sum(x => x.Cantidad);
    public decimal KgHilanderia    => Items.Where(x => x.CodServ == "C" || x.CodServ == "CT").Sum(x => x.Cantidad);
    public decimal KgTintoreria    => Items.Where(x => x.CodServ == "CT" || x.CodServ == "ST" ||
                                                        x.CodServ == "STD" || x.CodServ == "STR" ||
                                                        x.CodServ == "SRT").Sum(x => x.Cantidad);
    public int     PedidosHoy      => Items.Where(x => x.FchPedido.Date == DateTime.Today).Select(x => x.NumPed).Distinct().Count();
    public int     ItemsConVencido => Items.Count(x => x.EsVencido);
}
