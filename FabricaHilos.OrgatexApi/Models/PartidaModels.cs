namespace FabricaHilos.OrgatexApi.Models;

/// <summary>
/// Cabecera devuelta por el 1er result set de dbo.usp_ObtenerDatosPartida (ORGATEX).
/// </summary>
public sealed class PartidaCabecera
{
    public string?   NoRefPartida       { get; set; }
    public string?   Partida            { get; set; }
    public string?   Maquina            { get; set; }
    public string?   NombreMaquina      { get; set; }
    public int?      RecipeIdOrgatex    { get; set; } // BatchDetail.RecipeID: código real y estable de la receta (usar para COD_RECETA; RecetaNo/Name puede venir con texto libre pegado)
    public string?   RecetaNo           { get; set; }
    public string?   RecetaDesc         { get; set; }
    public string?   ColorNo            { get; set; }
    public string?   ColorNombre        { get; set; }
    public string?   Cliente            { get; set; }
    public string?   Calidad            { get; set; }
    public string?   CalidadDescription { get; set; }
    public decimal?  PesoLoteKg         { get; set; }
    public decimal?  RelacionBanioLxKg  { get; set; }
    public decimal?  CantidadAguaL      { get; set; }
    public DateTime? Queued             { get; set; }
    public DateTime? Loaded             { get; set; }
    public DateTime? Started            { get; set; }
    public DateTime? Terminated         { get; set; }
    public string?   FuenteDetalle      { get; set; } // COMPLETO | PARCIAL_SOLO_COLOR
}

/// <summary>
/// Línea de detalle devuelta por el 2do result set de dbo.usp_ObtenerDatosPartida (ORGATEX).
/// </summary>
public sealed class PartidaDetalle
{
    public int?     Llamada     { get; set; }
    public int?     Pos         { get; set; }
    public string?  ProductCode { get; set; }
    public string?  Descripcion { get; set; }
    public string?  Tipo        { get; set; }
    public decimal?  CantidadG  { get; set; }
    public string?  Unit        { get; set; }
    public string?  Modo        { get; set; }
    public string?  Fuente      { get; set; }
}

/// <summary>Resultado de aplicar una línea de detalle vía SP_MERGE_ING_RECETA.</summary>
public sealed class ResultadoLinea
{
    public int?    Llamada           { get; set; }
    public int?    Item              { get; set; }
    public string? ProductCode       { get; set; }
    public bool    Ok                { get; set; }
    public int     CodigoResultado   { get; set; }
    public string? MensajeResultado  { get; set; }
}

/// <summary>Resultado final devuelto por el endpoint de sincronización.</summary>
public sealed class ResultadoSincronizacion
{
    public string?  BatchRefNo        { get; set; }
    public string?  Partida           { get; set; }
    public string?  FuenteDetalle     { get; set; }
    public int      LineasOk          { get; set; }
    public int      LineasError       { get; set; }
    public bool     PartidaVinculada  { get; set; }
    public string?  MensajePartida    { get; set; }
    public List<ResultadoLinea> Lineas { get; set; } = [];
}
