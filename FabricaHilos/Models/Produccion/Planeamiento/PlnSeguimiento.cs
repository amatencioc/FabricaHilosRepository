namespace FabricaHilos.Models.Produccion.Planeamiento;

public class PlnSeguimiento
{
    public long   IdSeguim      { get; set; }
    public int    Serie         { get; set; }
    public long   NumPed        { get; set; }
    public int    Nro           { get; set; }
    public int    NumDet        { get; set; }

    public string? CodCliente   { get; set; }
    public string? NombreCliente { get; set; }
    public string? CodArt       { get; set; }
    public string? Color        { get; set; }
    public string? Titulo       { get; set; }
    public string? Proceso      { get; set; }
    public decimal CantidadOrig { get; set; }
    public string  SoloDespacho { get; set; } = "N";

    public string  CodPasoAct   { get; set; } = "01";
    public string? NombrePaso   { get; set; }
    public string? ColorUi      { get; set; }
    public string? CodPasoAnt   { get; set; }
    public int     NroCiclo     { get; set; } = 1;

    public DateTime  FchPedido        { get; set; }
    public DateTime? FchEntregaComp   { get; set; }

    // Fechas estimadas
    public DateTime? FchEstHilanderia { get; set; }
    public DateTime? FchEstPartida    { get; set; }
    public DateTime? FchEstTinIni     { get; set; }
    public DateTime? FchEstTinFin     { get; set; }
    public DateTime? FchEstSecado     { get; set; }
    public DateTime? FchEstCalidad    { get; set; }
    public DateTime? FchEstDespacho   { get; set; }

    // Fechas reales
    public DateTime? FchRealProgramado { get; set; }
    public DateTime? FchRealProduccion { get; set; }
    public DateTime? FchRealPartida    { get; set; }
    public DateTime? FchRealTinIni     { get; set; }
    public DateTime? FchRealTinFin     { get; set; }
    public DateTime? FchRealSecado     { get; set; }
    public DateTime? FchRealCcTinto    { get; set; }
    public DateTime? FchRealCcRechazo  { get; set; }
    public DateTime? FchRealDevanado   { get; set; }
    public DateTime? FchRealCalidad    { get; set; }
    public DateTime? FchRealAlmPt      { get; set; }
    public DateTime? FchRealDespacho   { get; set; }

    // KGs
    public decimal KgProducidos  { get; set; }
    public decimal KgEnTin       { get; set; }
    public decimal KgEnAlmPt     { get; set; }
    public decimal KgDespachados { get; set; }
    public decimal KgPendientes  { get; set; }

    // Indicadores
    public string IndRetraso   { get; set; } = "N";
    public int    DiasRetraso  { get; set; }
    public string IndUrgente   { get; set; } = "N";
    public string IndReproceso { get; set; } = "N";
    public string Estado       { get; set; } = "A";

    // Helpers
    public bool EstaRetrasado    => IndRetraso   == "S";
    public bool EsUrgente        => IndUrgente   == "S";
    public bool EstaEnReproceso  => IndReproceso == "S";
    public bool EstaCerrado      => Estado        == "C";
    public bool EsStock          => SoloDespacho  == "S";

    /// <summary>Porcentaje de avance en el flujo (0–100).</summary>
    public int PctAvance => CodPasoAct switch
    {
        "01"  =>  6,
        "02"  => 13,
        "03"  => 19,
        "04"  => 25,
        "05"  => 31,
        "06"  => 38,
        "07"  => 44,
        "08"  => 50,
        "09"  => 56,
        "09B" => 62,
        "10"  => 69,
        "11"  => 75,
        "12"  => 81,
        "13"  => 88,
        "14"  => 100,
        "9R"  => 50,
        _     =>  0
    };
}
