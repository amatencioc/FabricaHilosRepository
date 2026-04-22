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

public class OrdenCompraUploadModel
{
    public string?          TipoDocto       { get; set; }
    public int              Serie           { get; set; }
    public long             NumPed          { get; set; }
    public List<IFormFile>? Archivos        { get; set; }
    public List<string>     SeleccionItems  { get; set; } = new();   // formato: "COD_ART|ORDEN"
    public long?            ExistingIdGrupo { get; set; }

    public string? ReturnBuscar      { get; set; }
    public string? ReturnFechaInicio { get; set; }
    public string? ReturnFechaFin    { get; set; }
    public string? ReturnEstado      { get; set; }
    public int     ReturnPage        { get; set; } = 1;
}
