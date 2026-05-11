namespace FabricaHilos.Models.Logistica;

/// <summary>
/// Registro local (SQL Server) que persiste el resultado de cada O/C creada en Oracle.
/// Sirve como garantía de recuperación cuando la red cae después del COMMIT de Oracle
/// y antes de que la respuesta HTTP llegue al navegador.
/// </summary>
public class LogRegistroOc
{
    public long   Id          { get; set; }          // PK autoincremental
    public string Usuario     { get; set; } = "";    // OracleUser que registró
    public string TipoDocto   { get; set; } = "";
    public long   NumPed      { get; set; }          // número devuelto por Oracle
    public int    Serie       { get; set; } = 1;
    public string CodProveed  { get; set; } = "";
    public string Moneda      { get; set; } = "";
    public decimal Impsto     { get; set; }
    public DateTime Fecha     { get; set; }
    public DateTime FEntrega  { get; set; }
    public int    CantItems   { get; set; }          // cantidad de ítems enviados
    public string? Detalle    { get; set; }
    public DateTime FechaLog  { get; set; } = DateTime.UtcNow;
    public bool   Notificado  { get; set; } = false; // true una vez que el front confirmó recepción
}
