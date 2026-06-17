namespace FabricaHilos.Models.Sire;

/// <summary>
/// Resumen de resultados de conciliación entre SUNAT y Legacy (ERP Logix).
/// Leído desde SIG.SIRE_CONCIL_RESUMEN después de ejecutar SP_SIRE_CONCILIAR.
/// </summary>
public sealed class SireConcilResumen
{
    public int     TotalOk        { get; init; }   // Registros coinciden perfectamente
    public int     TotalDifer     { get; init; }   // Registros con diferencia de importe
    public int     TotalSoloSunat { get; init; }   // Registros en SUNAT pero no en Legacy
    public int     TotalSoloLeg   { get; init; }   // Registros en Legacy pero no en SUNAT
    public decimal DiffTotal      { get; init; }   // Diferencia neta de importes S/

    public int  TotalConciliados  => TotalOk + TotalDifer + TotalSoloSunat + TotalSoloLeg;
    public bool TieneProblemas    => TotalDifer > 0 || TotalSoloSunat > 0 || TotalSoloLeg > 0;

    /// Porcentaje de registros perfectamente ok (0-100)
    public double PctOk => TotalConciliados == 0 ? 0
        : Math.Round((double)TotalOk / TotalConciliados * 100, 1);
}
