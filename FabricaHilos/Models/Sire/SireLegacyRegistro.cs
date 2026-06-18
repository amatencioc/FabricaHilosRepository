namespace FabricaHilos.Models.Sire;

/// <summary>
/// Representa un registro cargado desde el ERP (Logix) a SIG.SIRE_LEGACY
/// por el SP SP_SIRE_CARGA_LEGACY.
/// Columnas: BASE_IMPONIBLE, IGV, OTROS, TOTAL (no BI_GRAV_DG / IGV_IPM_DG / TOTAL_CP).
/// </summary>
public sealed class SireLegacyRegistro
{
    public long      IdLegacy      { get; init; }
    public string    Tipo          { get; init; } = string.Empty;  // '1'=Ventas '2'=Compras
    public int       Periodo       { get; init; }
    public string?   TablaOrigen   { get; init; }  // 'DOCUVENT' | 'MOVGLOS'
    public string?   IdOrigen      { get; init; }

    // Comprobante
    public DateTime? FEmision      { get; init; }
    public DateTime? FVencto       { get; init; }
    public string?   Tipdoc        { get; init; }
    public string?   Serie         { get; init; }
    public string?   Numero        { get; init; }
    public string?   Tdocid        { get; init; }

    // Tercero
    public string?   Ruc           { get; init; }
    public string?   Nombre        { get; init; }

    // Importes — nombres exactos de BD: BASE_IMPONIBLE, IGV, OTROS, TOTAL
    public decimal   BaseImponible { get; init; }
    public decimal   Igv           { get; init; }
    public decimal   Otros         { get; init; }  // OTROS_TRIB (compras)
    public decimal   Total         { get; init; }

    // Campos adicionales v2 (necesarios para DIFF_CAMPOS completo)
    public decimal   Isc           { get; init; }
    public decimal   ValAdqNg      { get; init; }
    public decimal   ValFactGrat   { get; init; }
    public string?   TipoNota      { get; init; }  // ventas
    public string?   FlagDetrac    { get; init; }  // compras: 'D' o null
    public string?   AnioDam       { get; init; }  // compras: año DUA
    public string?   TipDocref     { get; init; }
    public string?   SerDocref     { get; init; }
    public string?   NroDocref     { get; init; }
    public DateTime? FDocref       { get; init; }

    // Tipo de cambio
    public string?   Moneda        { get; init; }
    public decimal   Cambio        { get; init; }

    // Referencia y estado ERP
    public string?   DocRef        { get; init; }
    public string?   EstErp        { get; init; }
    public string    Anulado       { get; init; } = "N";  // 'S' | 'N'

    // Cruce con SIRE_PROPUESTA
    public long?     IdPropMatch   { get; init; }
}
