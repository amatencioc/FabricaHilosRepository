namespace FabricaHilos.Models.Logistica;

public class OrdenCompraDto
{
    // ── Clave ──────────────────────────────────────────────────────────────
    public string?   TipoDocto      { get; set; }
    public int       Serie          { get; set; }
    public long      NumPed         { get; set; }

    // ── Cabecera ───────────────────────────────────────────────────────────
    public string?   Estado         { get; set; }
    public DateTime? Fecha          { get; set; }
    public string?   CodProveed     { get; set; }
    public string?   CondPag        { get; set; }
    public string?   Moneda         { get; set; }
    public string?   CodVende       { get; set; }
    public int?      PlazoEntrega   { get; set; }
    public string?   Detalle        { get; set; }
    public string?   CCosto         { get; set; }
    public DateTime? FEntrega       { get; set; }

    // ── Importes ───────────────────────────────────────────────────────────
    public decimal   ValVenta       { get; set; }
    public decimal   ImpDescto      { get; set; }
    public decimal   ImpNeto        { get; set; }
    public decimal   ImpIgv         { get; set; }
    public decimal   PrecioVta      { get; set; }
    public decimal   TotalFacturado { get; set; }

    // ── Aprobación gerencia ────────────────────────────────────────────────
    public string?   AprobGerencia  { get; set; }
    public DateTime? FAprobGer      { get; set; }

    // ── Auditoría ──────────────────────────────────────────────────────────
    public string?   AAduser        { get; set; }
    public DateTime? AAdfecha       { get; set; }
    public string?   AMduser        { get; set; }
    public DateTime? AMdfecha       { get; set; }
}

public class ItemOrdDto
{
    // ── Clave ──────────────────────────────────────────────────────────────
    public string?   TipoDocto     { get; set; }
    public int       Serie         { get; set; }
    public long      NumPed        { get; set; }
    public int       Orden         { get; set; }

    // ── Artículo ───────────────────────────────────────────────────────────
    public string?   CodArt        { get; set; }
    public string?   CodOrig       { get; set; }
    public string?   Unidad        { get; set; }
    public string?   Descripcion   { get; set; }
    public long?     NumReq        { get; set; }
    public int?      OrdenReq      { get; set; }

    // ── Cantidades ─────────────────────────────────────────────────────────
    public decimal   Cantidad      { get; set; }
    public decimal   Saldo         { get; set; }

    // ── Precio / importes ──────────────────────────────────────────────────
    public decimal   Precio        { get; set; }
    public decimal   ImpVvta       { get; set; }

    // ── Estado ─────────────────────────────────────────────────────────────
    public string?   Estado        { get; set; }

    // ── Grupo / aprobación ─────────────────────────────────────────────────
    public long?     IdGrupo       { get; set; }
    public DateTime? FAprobado     { get; set; }
}

// ── Registro Nueva Orden de Compra ─────────────────────────────────────────

public class RequisicionPendienteDto
{
    public string?   TipDoc          { get; set; }
    public int       Serie           { get; set; }
    public long      NumReq          { get; set; }
    public string?   CentroCosto     { get; set; }
    public string?   Proveedores     { get; set; }
    public DateTime? Fecha           { get; set; }
    public DateTime? FEntrega        { get; set; }
    public string?   Responsable     { get; set; }
    public string?   Prioridad       { get; set; }
    public string?   Observacion     { get; set; }
    public string?   Estado          { get; set; }
    public string?   Destino         { get; set; }
    public string?   IndServ         { get; set; }
    public string?   Autoriza        { get; set; }
    public int       TotalItems      { get; set; }
    public int       ItemsPendientes { get; set; }
}

public class ItemReqPendienteDto
{
    public string?   TipDoc         { get; set; }
    public int       Serie          { get; set; }
    public long      NumReq         { get; set; }
    public int       Orden          { get; set; }
    public string?   CodArt         { get; set; }
    public string?   Detalle        { get; set; }
    public string?   Unidad         { get; set; }
    public decimal   Cantidad       { get; set; }
    public decimal   Saldo          { get; set; }
    public string?   Moneda         { get; set; }
    public decimal   Precio         { get; set; }
    public string?   TpDestino      { get; set; }
    public string?   Destino        { get; set; }
    public string?   DestinoDesc    { get; set; }   // descripción resuelta desde P_OBTENER_DESTINOS
    public string?   CodSolicita    { get; set; }
    public string?   Marca          { get; set; }
    public string?   Observaciones  { get; set; }
    public string?   DescArticulo   { get; set; }
    public long      NumOcPrevio    { get; set; }
}

public class ItemSeleccionadoOcDto
{
    public string?   TipDoc     { get; set; }
    public int       Serie      { get; set; }
    public long      NumReq     { get; set; }
    public int       Orden      { get; set; }
    public string?   CodArt     { get; set; }
    public string?   Detalle    { get; set; }
    public string?   Unidad     { get; set; }
    public string?   CodOrig    { get; set; }
    public decimal   Cantidad   { get; set; }
    public decimal   Precio     { get; set; }
    public decimal   PorDesc1   { get; set; }
    public decimal   PorDesc2   { get; set; }
    public string?   TpDestino  { get; set; }
    public string?   Destino    { get; set; }
    public string?   CCodigo    { get; set; }
}

public class RegistrarOcRequest
{
    public string?   TipoDocto   { get; set; }
    public DateTime  Fecha       { get; set; }
    public DateTime  FEntrega    { get; set; }
    public string?   CodProveed  { get; set; }
    public string?   CondPag     { get; set; }
    public string?   Moneda      { get; set; }
    public decimal   Impsto      { get; set; }
    public string?   CCosto      { get; set; }
    public string?   Detalle     { get; set; }
    public string?   OpcLEntrega { get; set; }   // '1'=Dirección Actual  '2'=Otro Local
    public string?   LEntrega    { get; set; }
    public string?   CCodigo     { get; set; }
    public List<ItemSeleccionadoOcDto> Items { get; set; } = new();
}

public class AnularOcRequest
{
    public string?   TipoDocto  { get; set; }
    public long      NumPed     { get; set; }
}

public class OpcEntregaDto
{
    public string  OpcLEntrega  { get; set; } = "";
    public string  Descripcion  { get; set; } = "";
    public string? LEntregaRef  { get; set; }   // dirección real de la empresa (solo opción '1')
}

public class DestinoDto
{
    public string TpDestino  { get; set; } = "";   // 'U'=Centro de Costo  'A'=Activo Fijo
    public string Codigo     { get; set; } = "";
    public string Descripcion { get; set; } = "";
}

public class IgvDto
{
    public string  Codigo      { get; set; } = "";
    public string  Descripcion { get; set; } = "";
    public decimal Valor       { get; set; }
}

public class OrdenCompraUploadModel
{
    public string?          Dt              { get; set; }   // token cifrado: tipoDocto+serie+numPed
    public List<IFormFile>? Archivos        { get; set; }
    public List<string>     SeleccionItems  { get; set; } = new();   // formato: "COD_ART|ORDEN"
    public long?            ExistingIdGrupo { get; set; }

    public string? ReturnBuscar      { get; set; }
    public string? ReturnFechaInicio { get; set; }
    public string? ReturnFechaFin    { get; set; }
    public string? ReturnEstado      { get; set; }
    public int     ReturnPage        { get; set; } = 1;
}
