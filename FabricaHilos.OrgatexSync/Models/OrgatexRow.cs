namespace FabricaHilos.OrgatexSync.Models;

/// <summary>
/// Fila leída de ORGATEX (SQL Server: Dyelots + Dyelot_Recipe) lista para migrar
/// a Oracle SIG.CARGA_ORGATEX. Los nombres de propiedades siguen la misma
/// convención de alias usados en el SELECT origen (ver OrgatexRepository.SqlSelect).
/// </summary>
public sealed class OrgatexRow
{
    public decimal? RecetaOrgatex   { get; set; }
    public string?  Partida         { get; set; }
    public string?  CodColor        { get; set; }
    public string?  DescColor       { get; set; }
    public string?  Maquina         { get; set; }
    public decimal? Peso            { get; set; }
    public int?     Llamada         { get; set; }
    public int?     Contador        { get; set; }
    public string?  CodProducto     { get; set; }
    public string?  Descripcion     { get; set; }
    public decimal? CantOrgatex     { get; set; }
    public decimal? CantRealOrgatex { get; set; }
    public string?  Unidad          { get; set; }
    public DateTime Fecha           { get; set; }
}
