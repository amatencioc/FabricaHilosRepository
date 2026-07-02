namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Fila plana de un item de pedido con todos los campos relevantes:
/// cabecera de pedido + linea ITEMPED + articulo + familia/linea textil.
/// Se usa en la vista Registro de Pedidos (Index).
/// </summary>
public class RegistroPedidoItem
{
    // -- Pedido --
    public int      Serie       { get; set; }
    public long     NumPed      { get; set; }
    public DateTime FchPedido   { get; set; }
    public DateTime? FchAprobacion { get; set; }
    public string   CodCliente  { get; set; } = "";
    public string   NombreCliente { get; set; } = "";
    public string   CodVende    { get; set; } = "";
    public string   Giro        { get; set; } = "";
    public string   EstadoPed   { get; set; } = "";   // 0=sin aprobar 5=aprobado 9=cerrado

    // -- Item (ITEMPED) --
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
    public string   ColorDet    { get; set; } = "";   // descripcion libre del color
    public string   Intensidad  { get; set; } = "";
    public string   Presentacion { get; set; } = "";
    public string   EstadoItem  { get; set; } = "";
    public DateTime? FMaxPed    { get; set; }
    public string   SoloDespacho { get; set; } = "N";
    public string   Detalle     { get; set; } = "";

    // -- Articulo -> Familia / Linea --
    public string   CodFam      { get; set; } = "";
    public string   CodLin      { get; set; } = "";
    public string   DescFamilia { get; set; } = "";
    public string   DescLinea   { get; set; } = "";

    // -- Estado del proceso (PLN_SEGUIMIENTO) --
    public string   PasoActual      { get; set; } = "";
    public string   CodPasoAct      { get; set; } = "";
    public string   PasoActualColor { get; set; } = "#6c757d";
    public int      DiasRetraso     { get; set; }
    public string   IndRetraso      { get; set; } = "N";
    public string   IndUrgente      { get; set; } = "N";
    public long?    NumPartida      { get; set; }
    public decimal  KgPendientes    { get; set; }
    public DateTime? FchEntregaComp { get; set; }

    // -- Hilandería (ITEMPED_DET) --
    public long?    NroRmc          { get; set; }
    public string   Rmc             { get; set; } = "";
    public string   DescRmc         { get; set; } = "";
    public string   Lote            { get; set; } = "";
    public DateTime? FhcEntrega     { get; set; }
    public long?    Nroprog         { get; set; }
    public string   EstadoProg      { get; set; } = "";
    public string   Urgente         { get; set; } = "N";

    // -- Tipo de fibra textil (H_FIBRA) --
    // DescTfibra: backward-compat alias para DESC_FIBRA (antes V_TFIBRA, ahora H_FIBRA)
    public string   DescTfibra     { get; set; } = "";
    public string   DescFibra      { get => DescTfibra; set => DescTfibra = value; }

    // -- Nombre vendedor abreviado (TABLAS_AUXILIARES tipo=29) --
    public string   NombreVende     { get; set; } = "";

    // -- Intensidad abreviada (H_TPROD tabla='03') --
    public string   IntensidadAbrev { get; set; } = "";

    // -- Descripcion proceso (H_PROCESOS) --
    public string   NombreProcesoDb { get; set; } = "";

    // -- Backward-compat: Tfibra (antes ITEMPED.TFIBRA char1, ahora = TipoFibra) --
    public string   Tfibra { get => TipoFibra; }

    // -- Nuevos campos ITEMPED / ARTICUL --
    public string   ObservacionVenta { get; set; } = "";  // ITEMPED.OBSERVACIONES
    public string   ObservacionPcp   { get; set; } = "";  // ITEMPED_DET.OBSERVACIONES
    public string   Unidad        { get; set; } = "";
    public string   Enconado      { get; set; } = "";
    public string   Parafina      { get; set; } = "";

    // -- KG producción vs programado --
    public decimal  KgProgramados    { get; set; }   // SUM(ITEMPED_DET.CANTIDAD)
    public decimal  KgProducidos     { get; set; }   // SUM(LOTES.SALDO) via GUIA
    public decimal? PctMerma         { get; set; }   // (KgProducidos-KgProgramados)/KgProgramados×100
    public decimal? PctAlmPt         { get; set; }   // KgProducidos/KgProgramados×100

    // -- Lead Time: FCH_ENTREGA - F_APROBACION (días ventana planificada) --
    // NULL si el pedido no está aprobado o no tiene fecha de entrega
    public int?     LeadTime      { get; set; }

    // -- Computed helpers --

    /// Area logica derivada de COD_SERV
    public string AreaServicio => CodServ switch
    {
        "C"   => "Hilanderia",
        "CT"  => "Hilanderia + Tintoreria",
        "ST"  => "Solo Tintoreria",
        "STD" => "Tintoreria Directa",
        "STR" => "Tintoreria + Retorcido",
        "SR"  => "Retorcido",
        "SRT" => "Retorcido + Tintoreria",
        "SE"  => "Servicio Especial",
        "M"   => "Moulinex",
        _     when CodServ.StartsWith("S") => "Servicio " + CodServ,
        _     => CodServ
    };

    /// Nombre legible del proceso productivo (prioriza BD, fallback hardcoded)
    public string NombreProceso => !string.IsNullOrEmpty(NombreProcesoDb)
        ? NombreProcesoDb
        : Proceso switch
        {
            "01" => "Cardado",
            "20" => "Peinado",
            "24" => "Peinado Gaseado",
            _    => Proceso
        };

    /// Bootstrap color class para el badge de area
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
        "6" => "Cerrado",
        "9" => "Anulado",
        _   => EstadoPed
    };

    /// Indica si el item ya tiene fecha de entrega y esta vencido
    public bool EsVencido => FMaxPed.HasValue && FMaxPed.Value < DateTime.Today;
}

/// <summary>
/// ViewModel para la vista Registro de Pedidos.
/// </summary>
public class RegistroPedidosViewModel
{
    private IReadOnlyList<RegistroPedidoItem> _items = [];
    private int     _totalPedidos;
    private decimal _totalKg;
    private decimal _kgHilanderia;
    private decimal _kgTintoreria;
    private int     _pedidosHoy;
    private int     _itemsConVencido;

    public IReadOnlyList<RegistroPedidoItem> Items
    {
        get => _items;
        set { _items = value; ComputeKpis(); }
    }

    // -- Filtros activos --
    public DateTime? FchDesde         { get; set; }
    public DateTime? FchHasta         { get; set; }
    public DateTime? FchEntDesde      { get; set; }
    public DateTime? FchEntHasta      { get; set; }
    public string    FiltroServ       { get; set; } = "";
    public string    FiltroCliente    { get; set; } = "";
    public string    FiltroProceso    { get; set; } = "";
    public string    FiltroEstado     { get; set; } = "";
    public string    FiltroTfibra     { get; set; } = "";
    public string    FiltroPasoActual { get; set; } = "";
    public string    FiltroGrupo      { get; set; } = "dia";

    // -- KPIs (calculados en un unico pass al asignar Items) --
    public int     TotalPedidos    => _totalPedidos;
    public int     TotalItems      => _items.Count;
    public decimal TotalKg         => _totalKg;
    public decimal KgHilanderia    => _kgHilanderia;
    public decimal KgTintoreria    => _kgTintoreria;
    public int     PedidosHoy      => _pedidosHoy;
    public int     ItemsConVencido => _itemsConVencido;

    private void ComputeKpis()
    {
        var pedidos = new HashSet<long>(_items.Count);
        var pedHoy  = new HashSet<long>();
        decimal totalKg = 0, kgHil = 0, kgTint = 0;
        int     vencido = 0;
        var     hoy     = DateTime.Today;

        foreach (var x in _items)
        {
            pedidos.Add(x.NumPed);
            totalKg += x.Cantidad;

            if (x.CodServ == "C" || x.CodServ == "CT")
                kgHil += x.Cantidad;

            if (x.CodServ == "CT" || x.CodServ == "ST" ||
                x.CodServ == "STD" || x.CodServ == "STR" || x.CodServ == "SRT")
                kgTint += x.Cantidad;

            if (x.FchPedido.Date == hoy)
                pedHoy.Add(x.NumPed);

            if (x.FMaxPed.HasValue && x.FMaxPed.Value < hoy)
                vencido++;
        }

        _totalPedidos    = pedidos.Count;
        _totalKg         = totalKg;
        _kgHilanderia    = kgHil;
        _kgTintoreria    = kgTint;
        _pedidosHoy      = pedHoy.Count;
        _itemsConVencido = vencido;
    }
}
