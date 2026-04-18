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
