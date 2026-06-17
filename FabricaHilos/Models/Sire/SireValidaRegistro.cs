namespace FabricaHilos.Models.Sire;

/// <summary>
/// Representa un registro de propuesta SUNAT almacenado en SIG.SIRE_PROPUESTA.
/// </summary>
public sealed class SireValidaRegistro
{
    public long    IdProp      { get; init; }              // PK
    public string  CarSunat    { get; init; } = string.Empty;   // correlativo SUNAT
    public string  Tipo        { get; init; } = string.Empty;   // '1'=Ventas '2'=Compras
    public int     Periodo     { get; init; }                    // YYYYMM
    public DateTime? FEmision  { get; init; }
    public DateTime? FVencto   { get; init; }
    public string? Tipdoc      { get; init; }
    public string? Serie       { get; init; }
    public string? Numero      { get; init; }
    public string? Ruc         { get; init; }
    public string? Nombre      { get; init; }
    public decimal BiGravDg    { get; init; }
    public decimal IgvIpmDg    { get; init; }
    public decimal TotalCp     { get; init; }
    public string? Moneda      { get; init; }
    public decimal Cambio      { get; init; }
    public string? EstComp     { get; init; }   // estado de comprobante SUNAT
    public string? Inconsist   { get; init; }   // inconsistencia detectada por SUNAT
    public string? ConcilEstado { get; init; }  // '0'=Pendiente '1'=OK '2'=Diferencia etc.
    public string? ConcilDiffs  { get; init; }  // detalle de diferencias encontradas
    public DateTime? FchCarga  { get; init; }   // fecha en que se cargó desde SUNAT

    // ── Helpers de display ────────────────────────────────────────────────────
    public string EstCompLabel => EstComp switch
    {
        "1" => "Activo",  "2" => "Baja",   "3" => "Nulo",  _ => EstComp ?? "—"
    };
    public string EstCompBadge => EstComp switch
    {
        "1" => "bg-success", "2" => "bg-warning text-dark", "3" => "bg-danger", _ => "bg-secondary"
    };
    public string ConcilBadge => ConcilEstado switch
    {
        "1" => "bg-success",  "2" => "bg-warning text-dark",
        "3" => "bg-danger",   "4" => "bg-info text-dark",
        "5" => "bg-secondary", _ => "bg-light text-dark border"
    };
    public string ConcilLabel => ConcilEstado switch
    {
        "0" => "Pendiente", "1" => "OK",         "2" => "Diferencia",
        "3" => "Solo SUNAT","4" => "Solo Legacy", "5" => "Excluido", _ => "—"
    };
}
