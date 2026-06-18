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
    public string? AnioDam     { get; init; }   // Año DUA/DSI (importaciones)
    public string? Nrofin      { get; init; }   // Número final (rango)
    public string? Tdocid      { get; init; }   // Tipo doc identidad proveedor/cliente
    public string? Ruc         { get; init; }
    public string? Nombre      { get; init; }

    // Importes — destino gravadas (campo principal)
    public decimal BiGravDg    { get; init; }
    public decimal IgvIpmDg    { get; init; }

    // Importes — destino mixtas
    public decimal BiGravDgng  { get; init; }
    public decimal IgvIpmDgng  { get; init; }

    // Importes — destino no gravadas
    public decimal BiGravDng   { get; init; }
    public decimal IgvIpmDng   { get; init; }

    // Otros importes
    public decimal ValAdqNg    { get; init; }   // Valor adquisiciones no gravadas
    public decimal Isc         { get; init; }
    public decimal Icbper      { get; init; }
    public decimal OtrosTrib   { get; init; }
    public decimal TotalCp     { get; init; }

    // Tipo de cambio
    public string? Moneda      { get; init; }
    public decimal Cambio      { get; init; }

    // Documento de referencia
    public DateTime? FDocref   { get; init; }
    public string? TipDocref   { get; init; }
    public string? SerDocref   { get; init; }
    public string? NroDocref   { get; init; }

    // Campos adicionales SUNAT
    public string? FlagDetrac  { get; init; }   // S/N detracci&#xF3;n
    public string? TipoNota    { get; init; }   // Tipo nota cr&#xE9;dito/d&#xE9;bito

    // Estado SUNAT
    public string? EstComp     { get; init; }   // estado de comprobante SUNAT
    public string? Inconsist   { get; init; }   // inconsistencia detectada por SUNAT

    // Metadata de carga y conciliación
    public string? ConcilEstado { get; init; }  // '0'=Pendiente '1'=OK '2'=Diferencia etc.
    public string? ConcilDiffs  { get; init; }  // detalle de diferencias encontradas
    public DateTime? FchCarga  { get; init; }   // fecha en que se cargó desde SUNAT
    public DateTime? FchConcil { get; init; }   // fecha de última conciliación

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
