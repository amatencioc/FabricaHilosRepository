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

    // ── Importes ───────────────────────────────────────────────────────
    public decimal   Impsto         { get; set; }   // tasa IGV (ej: 0.18, -0.08, -0.10)
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
    public decimal   PorDesc1      { get; set; }
    public decimal   PorDesc2      { get; set; }
    public decimal   ImpVvta       { get; set; }

    // ── Estado ─────────────────────────────────────────────────────────────
    public string?   Estado        { get; set; }

    // ── Centro de Costo (del requerimiento origen) ─────────────────────────
    public string?   CCosto        { get; set; }

    // ── Grupo / aprobación ─────────────────────────────────────────────────
    public long?     IdGrupo       { get; set; }
    public DateTime? FAprobado     { get; set; }
}

// ── Detalle por destino (para Imprimir Contabilidad desagregado) ──────────

public class ItemOrdDestinoDto
{
    public long      NumReq        { get; set; }
    public int       OrdenReq      { get; set; }
    /// <summary>ITEMORD.ORDEN (= DESP_ITEMREQ.ORDEN_REF). Clave de matching con ItemOrdDto.Orden.</summary>
    public int       OrdenRef      { get; set; }
    public string?   CodArt        { get; set; }
    public string?   TpDestino     { get; set; }
    public string?   Destino       { get; set; }
    public string?   DestinoDesc   { get; set; }
    public string?   CodSolicita   { get; set; }
    public decimal   Cantidad      { get; set; }
    public decimal   Precio        { get; set; }
    public decimal   Importe       { get; set; }
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

public class PreviewBorradorRequest : RegistrarOcRequest
{
    // Mismo payload que RegistrarOcRequest pero sin validación de negocio;
    // incluye datos resueltos de UI para los lookups de nombre.
    public string? ProveedorNombre  { get; set; }
    public string? CondPagNombre    { get; set; }
    public string? CCostoNombre     { get; set; }
    public List<PreviewItemBorradorDto> ItemsConDesc { get; set; } = new();
}

public class PreviewItemBorradorDto : ItemSeleccionadoOcDto
{
    public string?   Desc         { get; set; }   // descripción visible del artículo
    public string?   DestinoDesc  { get; set; }   // descripción visible del destino
}

public class OpcEntregaDto
{
    public string  OpcLEntrega  { get; set; } = "";
    public string  Descripcion  { get; set; } = "";
    public string? LEntregaRef  { get; set; }   // dirección real de la empresa (solo opción '1')
}

public class ProveedorDetalleDto
{
    public string Codigo    { get; set; } = "";
    public string Nombre    { get; set; } = "";
    public string Ruc       { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Telefono  { get; set; } = "";
}

public class DestinoDto
{
    public string TpDestino  { get; set; } = "";   // 'U'=Centro de Costo  'A'=Activo Fijo
    public string Codigo     { get; set; } = "";
    public string Descripcion { get; set; } = "";
}

public class FirmaOcDto
{
    public string    Codigo         { get; set; } = "";
    public string    NombreCompleto { get; set; } = "";
    public string    Cargo          { get; set; } = "";
    public string    RolEtiqueta    { get; set; } = "";
    public byte[]?   Firma          { get; set; }

    // ── Campos extra devueltos por P_OBTENER_FIRMAS_OC ─────────────────
    /// <summary>FECHA_DOC: fecha de la O/C (cursor GENERADO).</summary>
    public DateTime? FechaDoc       { get; set; }
    /// <summary>APROB_GERENCIA: código de aprobación gerencia (cursor APROBADO).</summary>
    public string?   AprobGerencia  { get; set; }
    /// <summary>F_APROB_GER: fecha de aprobación gerencia (cursor APROBADO).</summary>
    public DateTime? FAprobGer      { get; set; }
}

public class IgvDto
{
    public string  Codigo      { get; set; } = "";
    public string  Descripcion { get; set; } = "";
    public decimal Valor       { get; set; }
    /// <summary>
    /// Verdadero cuando el tipo es una retención (I.RENTA): el valor es negativo
    /// y se resta del importe en lugar de sumarse.
    /// </summary>
    public bool    EsRetencion => Valor < 0;
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

/// <summary>
/// Ítem ya fusionado por CodArt + Unidad + Precio.
/// Usado por Imprimir.cshtml (proveedor). Cualquier cambio en la lógica
/// de fusión debe hacerse aquí y se refleja automáticamente en todos los consumidores.
/// </summary>
public class MergedItemOrdDto
{
    public string  CodArt      { get; init; } = "";
    public string  Unidad      { get; init; } = "";
    public decimal Precio      { get; init; }
    public decimal Cantidad    { get; init; }
    public decimal ImpVvta     { get; init; }
    public decimal PorDesc1    { get; init; }
    public decimal PorDesc2    { get; init; }
    public string? Descripcion { get; init; }
    public string? CodOrig     { get; init; }

    /// <summary>Pares (NumReq, Orden) de los ítems origen del grupo, ordenados desc por NumReq.</summary>
    public List<(long NumReq, int Orden)> ReqItems { get; init; } = [];

    // ── Método de fusión — fuente única de verdad ─────────────────────────
    /// <summary>
    /// Fusiona una lista de <see cref="ItemOrdDto"/> agrupando por
    /// CodArt + Unidad + Precio. Cantidades e importes se suman.
    /// Los ítems sin req (NumReq nulo o 0) también se incluyen pero sin badge REQ.
    /// </summary>
    public static List<MergedItemOrdDto> FusionarItems(IEnumerable<ItemOrdDto> items) =>
        items
            .GroupBy(i => new
            {
                CodArt = (i.CodArt ?? "").Trim(),
                Unidad = (i.Unidad ?? "").Trim(),
                Precio = i.Precio
            })
            .Select(g => new MergedItemOrdDto
            {
                CodArt      = g.Key.CodArt,
                Unidad      = g.Key.Unidad,
                Precio      = g.Key.Precio,
                Cantidad    = g.Sum(i => i.Cantidad),
                ImpVvta     = g.Sum(i => i.ImpVvta),
                PorDesc1    = g.First().PorDesc1,
                PorDesc2    = g.First().PorDesc2,
                Descripcion = g.First().Descripcion,
                CodOrig     = g.First().CodOrig,
                ReqItems    = g
                    .Where(i => i.NumReq.HasValue && i.NumReq.Value > 0)
                    .Select(i => (i.NumReq!.Value, i.Orden))
                    .OrderByDescending(x => x.Value).ThenBy(x => x.Orden)
                    .ToList()
            })
            .OrderBy(x => x.CodArt)
            .ToList();
}

/// <summary>
/// Resultado de comparación OC vs. ingresos de almacén (KARDEX) por ítem.
/// </summary>
public class IngresoAlmacenItemDto
{
    public int       Orden          { get; set; }       // Orden del ítem en la OC
    public long?     NumReq         { get; set; }       // Requerimiento de origen
    public int?      OrdenReq       { get; set; }       // Ítem del requerimiento
    public string?   CodArt         { get; set; }
    public string?   Descripcion    { get; set; }
    public decimal   QtyOc          { get; set; }       // Cantidad pedida en OC
    public decimal   PrecioUnit     { get; set; }
    public decimal   ImporteOc      { get; set; }
    public decimal   QtyIngresada   { get; set; }       // Suma de kardex
    public decimal   QtyPendiente   { get; set; }       // QtyOc - QtyIngresada
    public decimal   PctIngresado   { get; set; }       // %
    public int       CantIngresos   { get; set; }       // N° kardex distintos
    public string?   UltFchIngreso  { get; set; }       // DD/MM/YYYY
    public string?   Operario       { get; set; }
    public string?   Comprobante    { get; set; }       // TipDoc-Serie-Numero
    public string    Estado         { get; set; } = "PENDIENTE";  // COMPLETO / PARCIAL / PENDIENTE
}
