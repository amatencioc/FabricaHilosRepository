namespace FabricaHilos.Models.Sire;

/// <summary>
/// Resumen de resultados de conciliación entre SUNAT y Legacy (ERP Logix).
/// Leído desde SIG.SIRE_CONCIL_RESUMEN después de ejecutar SP_SIRE_CONCILIAR.
/// Columnas exactas de la tabla definidas en 01_SIRE_TABLAS.sql.
/// </summary>
public sealed class SireConcilResumen
{
    // Conteos por estado de conciliación
    public int     TotalSunat     { get; init; }   // Registros en SUNAT
    public int     TotalLegacy    { get; init; }   // Registros en Legacy
    public int     TotalOk        { get; init; }   // Registros coinciden perfectamente
    public int     TotalDifer     { get; init; }   // Registros con diferencia de importe
    public int     TotalSoloSunat { get; init; }   // En SUNAT pero no en Legacy
    public int     TotalSoloLeg   { get; init; }   // En Legacy pero no en SUNAT
    public int     TotalExcl      { get; init; }   // Excluidos de la conciliación

    // Sumas SUNAT
    public decimal SumaSunatBase  { get; init; }
    public decimal SumaSunatIgv   { get; init; }
    public decimal SumaSunatTotal { get; init; }

    // Sumas Legacy
    public decimal SumaLegBase    { get; init; }
    public decimal SumaLegIgv     { get; init; }
    public decimal SumaLegTotal   { get; init; }

    // Diferencias acumuladas (SUNAT - Legacy)
    public decimal DiffBase       { get; init; }
    public decimal DiffIgv        { get; init; }
    public decimal DiffTotal      { get; init; }

    // Metadata del proceso
    public DateTime? FchConcil    { get; init; }
    public string?   ConcilPor    { get; init; }

    // Estado del período
    public string    EstadoCierre { get; init; } = "ABIERTO";  // 'ABIERTO' | 'CERRADO' | 'OBSERVADO'

    // ── Computed ────────────────────────────────────────────────────────────
    public int  TotalConciliados  => TotalOk + TotalDifer + TotalSoloSunat + TotalSoloLeg;
    public bool TieneProblemas    => TotalDifer > 0 || TotalSoloSunat > 0 || TotalSoloLeg > 0;

    /// Porcentaje de registros perfectamente ok (0-100)
    public double PctOk => TotalConciliados == 0 ? 0
        : Math.Round((double)TotalOk / TotalConciliados * 100, 1);

    public string EstadoCierreBadgeCss => EstadoCierre switch
    {
        "CERRADO"   => "badge bg-success",
        "OBSERVADO" => "badge bg-warning text-dark",
        _           => "badge bg-secondary"
    };
}
