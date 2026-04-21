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
    // Ítems que tienen ID_GRUPO y F_APROBADO (aprobación interna de logística)
    public bool Etapa1Aprobada  { get; set; }
    public int  Etapa1Items     { get; set; }   // cantidad de ítems con F_APROBADO

    // ── Etapa 2: Orden de compra ──────────────────────────────────────────────
    // Ítems que tienen una orden de compra emitida
    // TODO: mapear desde tabla/campo correspondiente cuando esté disponible
    public bool Etapa2Aprobada  { get; set; }
    public int  Etapa2Items     { get; set; }

    // ── Etapa 3: Facturado ────────────────────────────────────────────────────
    // Orden de compra ya facturada (existe en REGISTRO_DIARIO con NUM_REF=NRO_DOC_REF AND TIPO='RS')
    public bool    Etapa3Aprobada  { get; set; }
    public int     Etapa3Items     { get; set; }
    // Datos del comprobante obtenido de REGISTRO_DIARIO
    public string? Etapa3TipDoc    { get; set; }
    public string? Etapa3Serie     { get; set; }
    public string? Etapa3Numero    { get; set; }
    public string? Etapa3Relacion  { get; set; }

    // ── Etapa 4: Pendiente de pago ────────────────────────────────────────────
    // Pago: registro en FACTPAG WHERE tipdoc/serie_NUM/numero/cod_proveedor de Etapa3
    public bool     Etapa4Aprobada { get; set; }
    public int      Etapa4Items    { get; set; }
    public decimal? Etapa4Saldo    { get; set; }

    /// <summary>Etapa actual alcanzada (0 = ninguna, 1–4)</summary>
    public int EtapaActual =>
        Etapa4Aprobada ? 4 :
        Etapa3Aprobada ? 3 :
        Etapa2Aprobada ? 2 :
        Etapa1Aprobada ? 1 : 0;

    /// <summary>Porcentaje de avance (25% por etapa)</summary>
    public int PorcentajeGeneral => EtapaActual * 25;
}
