namespace FabricaHilos.Models.Sire;

/// <summary>
/// Resumen de una propuesta SUNAT descargada en SIRE_PROPUESTA, agrupada por TIPO+PERIODO.
/// Usada en las vistas de Ventas/Compras para mostrar el listado de archivos disponibles.
/// </summary>
public sealed class PropuestaPeriodoResumen
{
    public string   Tipo            { get; init; } = string.Empty;  // '1'=Ventas '2'=Compras
    public int      Periodo         { get; init; }                   // YYYYMM
    public string?  JobId           { get; init; }                   // último job que cargó
    public int      TotalRegistros  { get; init; }
    public DateTime? FchCarga       { get; init; }
    public decimal  TotalBase       { get; init; }
    public decimal  TotalIgv        { get; init; }
    public decimal  TotalImporte    { get; init; }
    public string   ConcilEstado    { get; init; } = "0";           // estado de conciliación

    public string PeriodoLabel => Periodo.ToString() is { Length: 6 } s
        ? $"{s[..4]}/{s[4..]}" : Periodo.ToString();

    public string ConcilBadgeCss => ConcilEstado switch
    {
        "1" => "bg-success",
        "2" => "bg-warning text-dark",
        "3" or "4" => "bg-danger",
        "5" => "bg-secondary",
        _   => "bg-light text-dark border"
    };
    public string ConcilLabel => ConcilEstado switch
    {
        "0" => "Sin conciliar",
        "1" => "OK",
        "2" => "Con diferencias",
        "3" => "Solo SUNAT",
        "4" => "Solo Legacy",
        "5" => "Excluido",
        _   => "—"
    };
}
