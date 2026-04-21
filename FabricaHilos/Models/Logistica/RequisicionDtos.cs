namespace FabricaHilos.Models.Logistica;

public class RequisicionDto
{
    // ── Clave ─────────────────────────────────────────────────────────────────
    public string?   TipDoc           { get; set; }
    public int       Serie            { get; set; }
    public long      NumReq           { get; set; }

    // ── Cabecera principal ────────────────────────────────────────────────────
    public string?   CentroCosto      { get; set; }
    public string?   Proveedores      { get; set; }
    public DateTime? Fecha            { get; set; }
    public DateTime? FEntrega         { get; set; }
    public string?   Responsable      { get; set; }
    public string?   Prioridad        { get; set; }
    public string?   Observacion      { get; set; }
    public string?   Estado           { get; set; }
    public string?   Destino          { get; set; }
    public string?   IndServ          { get; set; }

    // ── Impuestos / afectaciones ──────────────────────────────────────────────
    public decimal?  Impsto           { get; set; }
    public string?   AfectoIgv        { get; set; }
    public string?   AfectoIrenta     { get; set; }

    // ── Referencia ────────────────────────────────────────────────────────────
    public string?   TipRef           { get; set; }
    public int?      SerRef           { get; set; }
    public long?     NroRef           { get; set; }

    // ── Autorización ─────────────────────────────────────────────────────────
    public DateTime? FAutoriza        { get; set; }
    public string?   Autoriza         { get; set; }
    public string?   UserAutoriza     { get; set; }
    public string?   IpAutoriza       { get; set; }

    // ── Recepción ─────────────────────────────────────────────────────────────
    public DateTime? FRecibe          { get; set; }
    public string?   Recibe           { get; set; }

    // ── Progreso (ítems con grupo vs total) ──────────────────────────────────
    public int TotalItems    { get; set; }
    public int ItemsConGrupo { get; set; }
    /// <summary>Progreso de ítems con documento adjunto (aprobación interna)</summary>
    public int Progreso => TotalItems == 0 ? 0 : (int)Math.Round(ItemsConGrupo * 100.0 / TotalItems);

    /// <summary>Progreso general del requerimiento por las 4 etapas del flujo logístico</summary>
    public ProgresoGeneralDto ProgresoGeneral { get; set; } = new();

    /// <summary>Números de órdenes de compra distintas asociadas al requerimiento</summary>
    public List<string> OrdenesCompra { get; set; } = new();

    // ── Logística ─────────────────────────────────────────────────────────────
    public DateTime? FchEntregaLogist { get; set; }
    public string?   NotaAnulacion    { get; set; }

    // ── Auditoría ─────────────────────────────────────────────────────────────
    public string?   AAduser          { get; set; }
    public DateTime? AAdfecha         { get; set; }
    public string?   AMduser          { get; set; }
    public DateTime? AMdfecha         { get; set; }
}

public class ItemReqDto
{
    // ── Clave ─────────────────────────────────────────────────────────────────
    public string?   TipDoc        { get; set; }
    public int       Serie         { get; set; }
    public long      NumReq        { get; set; }
    public int       Orden         { get; set; }

    // ── Artículo ──────────────────────────────────────────────────────────────
    public string?   CodArt        { get; set; }
    public string?   Detalle       { get; set; }
    public string?   Unidad        { get; set; }
    public string?   Marca         { get; set; }
    public string?   CtaCtble      { get; set; }

    // ── Cantidades ────────────────────────────────────────────────────────────
    public decimal   Cantidad      { get; set; }
    public decimal   Saldo         { get; set; }
    public decimal?  StkMin        { get; set; }
    public decimal?  StkHist       { get; set; }

    // ── Precio / moneda ───────────────────────────────────────────────────────
    public string?   Moneda        { get; set; }
    public decimal   Precio        { get; set; }

    // ── Destino / solicitante ─────────────────────────────────────────────────
    public string?   TpDestino     { get; set; }
    public string?   Destino       { get; set; }
    public string?   CodSolicita   { get; set; }

    // ── Grupo / aprobación ────────────────────────────────────────────────────
    public long?     IdGrupo       { get; set; }
    public DateTime? FAprobado     { get; set; }
    public string?   NroDocRef     { get; set; }  // Nº de Orden de Compra (de DESP_ITEMREQ)

    // ── Observaciones ─────────────────────────────────────────────────────────
    public string?   Observaciones { get; set; }

    // ── Auditoría ─────────────────────────────────────────────────────────────
    public string?   AAduser       { get; set; }
    public DateTime? AAdfecha      { get; set; }
    public string?   AMduser       { get; set; }
    public DateTime? AMdfecha      { get; set; }
}

public class RequisicionDetalleViewModel
{
    public RequisicionDto         Cabecera { get; set; } = new();
    public List<ItemReqDto>       Items    { get; set; } = new();
    public RequisicionUploadModel Upload   { get; set; } = new();
}

public class RequisicionUploadModel
{
    public string?          TipDoc          { get; set; }
    public int              Serie           { get; set; }
    public long             NumReq          { get; set; }
    public List<IFormFile>? Archivos        { get; set; }
    public List<int>        OrdenesItems    { get; set; } = new();
    public long?            ExistingIdGrupo { get; set; }

    // Filtros del listado — se preservan al navegar a/desde el detalle
    public string? ReturnBuscar      { get; set; }
    public string? ReturnFechaInicio { get; set; }
    public string? ReturnFechaFin    { get; set; }
    public string? ReturnEstado      { get; set; }
    public int     ReturnPage        { get; set; } = 1;
}

public class ArchivoRequisicionDto
{
    public string   NombreArchivo { get; set; } = string.Empty;
    public string   RutaRelativa  { get; set; } = string.Empty;
    public long     TamanioBytes  { get; set; }
    public DateTime FechaCarga    { get; set; }
    public long     IdGrupo       { get; set; }
    public string   CarpetaGrupo  { get; set; } = string.Empty;
}

/// <summary>
/// Progreso general del requerimiento a través de las 4 etapas del flujo logístico.
/// Cada etapa indica si al menos un ítem del requerimiento ha alcanzado ese estado.
/// </summary>
public class ProgresoGeneralDto
{
    // ── Etapa 1: Aprobación del requerimiento ─────────────────────────────────
    // Un grupo se considera aprobado cuando tiene F_APROBADO. La etapa está
    // completa solo cuando TODOS los grupos distintos han sido aprobados.
    public int  Etapa1GruposTotal     { get; set; }   // grupos distintos con ID_GRUPO
    public int  Etapa1GruposAprobados { get; set; }   // grupos que tienen F_APROBADO
    public bool Etapa1Aprobada  => Etapa1GruposTotal > 0 && Etapa1GruposAprobados == Etapa1GruposTotal;
    public int  Etapa1Items     => Etapa1GruposAprobados;  // compatibilidad con la vista

    // ── Etapa 2: Orden de compra ──────────────────────────────────────────────
    // La etapa está completa cuando TODOS los ítems del requerimiento tienen O/C.
    public int          Etapa2ItemsTotal    { get; set; }  // ítems del requerimiento
    public int          Etapa2ItemsConOC    { get; set; }  // ítems con NRO_DOC_REF
    public bool         Etapa2Aprobada  => Etapa2ItemsTotal > 0 && Etapa2ItemsConOC == Etapa2ItemsTotal;
    public int          Etapa2Items     => Etapa2ItemsConOC;
    public List<string> OrdenesCompra   { get; set; } = new();  // NRO_DOC_REF distintos

    // ── Etapa 3: Facturado ────────────────────────────────────────────────────
    // Completa cuando TODAS las O/C del requerimiento tienen factura en REGISTRO_DIARIO TIPO='RS'.
    // OC_TOTAL = O/C distintas en DESP_ITEMREQ; OC_FACTURADAS = de esas, cuántas tienen NUM_REF en REGISTRO_DIARIO.
    public int  Etapa3OcTotal      { get; set; }  // O/C distintas (DESP_ITEMREQ.NRO_DOC_REF)
    public int  Etapa3OcFacturadas { get; set; }  // O/C que tienen factura (REGISTRO_DIARIO TIPO='RS')
    public bool Etapa3Aprobada     => Etapa3OcTotal > 0 && Etapa3OcFacturadas == Etapa3OcTotal;
    public int  Etapa3Items        => Etapa3OcFacturadas;

    // ── Etapa 4: Pago ─────────────────────────────────────────────────────────
    // Completa cuando TODAS las facturas de las O/C tienen SALDO=0 en FACTPAG.
    // FACT_TOTAL = facturas distintas en REGISTRO_DIARIO; FACT_PAGADAS = con SALDO=0 en FACTPAG.
    public int  Etapa4FacturasTotal  { get; set; }  // facturas distintas de REGISTRO_DIARIO
    public int  Etapa4FacturasPagadas{ get; set; }  // facturas con SALDO=0 en FACTPAG
    public bool Etapa4Aprobada       => Etapa4FacturasTotal > 0 && Etapa4FacturasPagadas == Etapa4FacturasTotal;
    public int  Etapa4Items          => Etapa4FacturasPagadas;

    /// <summary>Etapa actual alcanzada (0 = ninguna, 1–4)</summary>
    public int EtapaActual =>
        Etapa4Aprobada ? 4 :
        Etapa3Aprobada ? 3 :
        Etapa2Aprobada ? 2 :
        Etapa1Aprobada ? 1 : 0;

    /// <summary>
    /// Porcentaje de avance total (0–100).
    /// Cada etapa completa aporta 25%. Dentro de la Etapa1, el avance parcial
    /// se calcula proporcionalmente por grupos aprobados / grupos totales.
    /// </summary>
    public int PorcentajeGeneral
    {
        get
        {
            // Etapas 2-4 completadas suman desde Etapa1 completa
            int baseEtapas = EtapaActual * 25;
            if (EtapaActual >= 1) return baseEtapas; // Etapa1 ya completa

            // Progreso parcial dentro de Etapa1 (0–25%)
            if (Etapa1GruposTotal > 0)
                return (int)Math.Round(Etapa1GruposAprobados * 25.0 / Etapa1GruposTotal);

            return 0;
        }
    }
}
