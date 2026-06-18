namespace FabricaHilos.Models.Sire;

/// <summary>
/// Documento SOLO_SUNAT excluido del cruce con el ERP Logix.
/// Mapeado a SIG.SIRE_EXCLUIDOS_LOGIX.
/// </summary>
public class SireExcluidoLogix
{
    public long     IdExcluido    { get; init; }
    public string   Tipo          { get; init; } = "";   // '1'=Ventas '2'=Compras
    public int      Periodo       { get; init; }
    public string   Motivo        { get; init; } = "";   // 'NC_AUTO' | 'MANUAL'

    // Snapshot del documento
    public long?    IdProp        { get; init; }
    public long?    IdConcil      { get; init; }
    public string?  Tipdoc        { get; init; }
    public string?  Serie         { get; init; }
    public string?  Numero        { get; init; }
    public DateTime? FEmision     { get; init; }
    public string?  Ruc           { get; init; }
    public string?  Nombre        { get; init; }
    public decimal  TotalCp       { get; init; }
    public string?  Moneda        { get; init; }

    // Referencia (para N/C)
    public string?  TipDocref     { get; init; }
    public string?  SerDocref     { get; init; }
    public string?  NroDocref     { get; init; }

    // Vínculo par N/C ↔ doc. original (ambos en esta tabla)
    public long?    IdExcluidoRel { get; init; }

    // Auditoría
    public string?  Usuario       { get; init; }
    public DateTime FchExclusion  { get; init; }
    public string?  Obs           { get; init; }
    public string   Estado        { get; init; } = "A"; // 'A'=Activo 'R'=Restaurado

    // -------------------------------------------------------------------------
    // Helpers de presentación
    // -------------------------------------------------------------------------
    public string MotivoLabel => Motivo switch
    {
        "NC_AUTO" => "N/C automática",
        "MANUAL"  => "Exclusión manual",
        _         => Motivo
    };

    public string MotivoIconClass => Motivo switch
    {
        "NC_AUTO" => "bi bi-scissors text-warning",
        "MANUAL"  => "bi bi-hand-index-thumb text-secondary",
        _         => "bi bi-dash"
    };

    public bool EstaActivo => Estado == "A";
}
