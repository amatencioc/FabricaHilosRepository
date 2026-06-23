namespace FabricaHilos.Models.Sire;

/// <summary>
/// Representa una fila de reconciliación cruzada almacenada en SIG.SIRE_CONCIL.
/// ESTADO (VARCHAR2 real de BD): 'OK' | 'DIFERENCIA' | 'SOLO_SUNAT' | 'SOLO_LEGACY' | 'EXCLUIDO' | 'PENDIENTE'
/// </summary>
public sealed class SireConcilDetalle
{
    public long      IdConcil     { get; init; }
    public string    Tipo         { get; init; } = string.Empty;
    public int       Periodo      { get; init; }
    public long?     IdProp       { get; init; }
    public long?     IdLegacy     { get; init; }

    // Datos identificadores (tomados de SUNAT o Legacy según disponibilidad)
    public string?   Tipdoc       { get; init; }
    public string?   Serie        { get; init; }
    public string?   Numero       { get; init; }
    public DateTime? FEmision     { get; init; }
    public string?   Ruc          { get; init; }
    public string?   Nombre       { get; init; }

    // Estado de reconciliación — valor texto exacto de la BD
    public string    Estado       { get; init; } = string.Empty;

    // Importes SUNAT — columnas: SUNAT_BASE, SUNAT_IGV, SUNAT_TOTAL, SUNAT_MONEDA, SUNAT_EST
    public decimal   SunatBase    { get; init; }
    public decimal   SunatIgv     { get; init; }
    public decimal   SunatTotal   { get; init; }
    public string?   SunatMoneda  { get; init; }
    public string?   SunatEst     { get; init; }   // EST_COMP de SIRE_PROPUESTA (1=Activo 2=Baja 3=Nulo)

    // Importes Legacy — columnas: LEG_BASE, LEG_IGV, LEG_TOTAL, LEG_MONEDA, LEG_EST
    public decimal   LegBase      { get; init; }
    public decimal   LegIgv       { get; init; }
    public decimal   LegTotal     { get; init; }
    public string?   LegMoneda    { get; init; }
    public string?   LegEst       { get; init; }

    // Diferencias — columnas reales: DIFF_TOTAL_CP, DIFF_BASE, DIFF_IGV, DIFF_FECHA, DIFF_CAMPOS
    public decimal   DiffTotalCp  { get; init; }
    public decimal   DiffBase     { get; init; }
    public decimal   DiffIgv      { get; init; }
    public int?      DiffFecha    { get; init; }   // diferencia en días de emisión
    public string?   DiffCampos   { get; init; }   // pipe-separated: "BIGRAVDG|VALADQNG|..."

    // Importes adicionales para mostrar comparación campo a campo
    public decimal   SunatValAdqNg { get; init; }
    public decimal   LegValAdqNg   { get; init; }
    public decimal   SunatIsc      { get; init; }
    public decimal   LegIsc        { get; init; }
    public decimal   SunatOtros    { get; init; }
    public decimal   LegOtros      { get; init; }
    public decimal?  SunatCambio   { get; init; }
    public decimal?  LegCambio     { get; init; }

    // Referencia (doc. al que modifica: útil para NC/ND)
    public string?   TipDocref    { get; init; }   // TIP_DOCREF
    public string?   SerDocref    { get; init; }   // SER_DOCREF
    public string?   NroDocref    { get; init; }   // NRO_DOCREF
    public DateTime? FDocref      { get; init; }   // F_DOCREF
    public string?   TipoNota     { get; init; }   // TIPO_NOTA

    /// <summary>True si el RUC del comprobante aparece en el padrón SSCO de SUNAT.</summary>
    public bool      EsSsco       { get; init; }

    // Auditoría de exclusión (de SIRE_EXCLUIDOS_LOGIX — solo cuando ESTADO='EXCLUIDO')
    public string?   ExclMotivo   { get; init; }   // NC_AUTO | MANUAL
    public string?   ExclObs      { get; init; }   // Observación del usuario
    public string?   ExclUsuario  { get; init; }   // Usuario que excluyó
    public DateTime? ExclFch      { get; init; }   // Fecha de exclusión

    // Revisión manual
    public string    Revisado     { get; init; } = "N";
    public string?   ObsManual    { get; init; }

    // Validez de comprobante (API Consulta Integrada SUNAT)
    public string?   ValidezCp    { get; init; }   // 0=NO EXISTE 1=ACEPTADO 2=ANULADO 3=AUTORIZADO 4=NO AUTORIZADO
    public string?   ValidezRuc   { get; init; }   // 00=ACTIVO 01=BAJA PROVISIONAL 10=BAJA DEFINITIVA ...
    public string?   ValidezDom   { get; init; }   // 00=HABIDO 09=PENDIENTE 12=NO HABIDO ...
    public DateTime? FchValidez   { get; init; }   // Fecha última validación

    // Tipo de cambio para conversión a PEN al llamar a SUNAT (SunatMoneda ya existe arriba)
    public decimal   CambioMoneda { get; init; } = 1m;  // Tipo de cambio a PEN (1 si ya es PEN)

    /// <summary>Badge Bootstrap para el estadoCp de SUNAT.</summary>
    public string ValidezCpBadge => ValidezCp switch
    {
        "1" => "bg-success",
        "2" => "bg-danger",
        "0" => "bg-warning text-dark",
        "3" => "bg-info text-dark",
        "4" => "bg-danger",
        _   => "bg-secondary"
    };
    public string ValidezCpLabel => ValidezCp switch
    {
        "1" => "ACEPTADO",
        "2" => "ANULADO",
        "0" => "NO EXISTE",
        "3" => "AUTORIZADO",
        "4" => "NO AUTOR.",
        _   => ValidezCp != null ? $"CP:{ValidezCp}" : "-"
    };
    public string ValidezRucLabel => ValidezRuc switch
    {
        "00" => "ACTIVO",
        "01" => "BAJA PROV.",
        "02" => "BAJA OFICIO",
        "03" => "SUSPENSO",
        "10" => "BAJA DEFIN.",
        "11" => "BAJA OFICIO",
        "22" => "INHABILITADO",
        _   => ValidezRuc ?? "-"
    };
    public string ValidezDomLabel => ValidezDom switch
    {
        "00" => "HABIDO",
        "09" => "PENDIENTE",
        "11" => "POR VERIF.",
        "12" => "NO HABIDO",
        "20" => "NO HALLADO",
        _   => ValidezDom ?? "-"
    };

    // ── Helpers de display ──────────────────────────────────────────────────
    public string EstadoLabel => Estado switch
    {
        "OK"          => "OK",
        "AVISO"       => "Con Aviso",
        "DIFERENCIA"  => "Diferencia",
        "SOLO_SUNAT"  => "Solo SUNAT",
        "SOLO_LEGACY" => "Solo Legacy",
        "EXCLUIDO"    => "Excluido",
        "PENDIENTE"   => "Pendiente",
        _             => Estado
    };

    public string EstadoBadgeCss => Estado switch
    {
        "OK"          => "sire-badge sire-badge-ok",
        "AVISO"       => "sire-badge sire-badge-warn",
        "DIFERENCIA"  => "sire-badge sire-badge-danger",
        "SOLO_SUNAT"  => "sire-badge sire-badge-rce",
        "SOLO_LEGACY" => "sire-badge sire-badge-legacy",
        "EXCLUIDO"    => "sire-badge sire-badge-secondary",
        _             => "sire-badge sire-badge-secondary"
    };

    /// <summary>
    /// Dado un código de campo (ej: "BIGRAVDG"), devuelve (sunatVal, legVal) para el tooltip.
    /// </summary>
    public (string S, string L) ValoresCampo(string campo) => campo switch
    {
        "BIGRAVDG"   => (SunatBase.ToString("N2"),      LegBase.ToString("N2")),
        "IGVIPMDG"   => (SunatIgv.ToString("N2"),       LegIgv.ToString("N2")),
        "TOTAL_CP"   => (SunatTotal.ToString("N2"),     LegTotal.ToString("N2")),
        "VALADQNG"   => (SunatValAdqNg.ToString("N2"),  LegValAdqNg.ToString("N2")),
        "ISC"        => (SunatIsc.ToString("N2"),       LegIsc.ToString("N2")),
        "OTROSTRIB"  => (SunatOtros.ToString("N2"),     LegOtros.ToString("N2")),
        "MONEDA"     => (SunatMoneda ?? "-",            LegMoneda ?? "-"),
        "EST.COMP"   => (SunatEst ?? "-",               LegEst ?? "-"),
        "T.CAMBIO"   => (SunatCambio?.ToString("N4") ?? "-", LegCambio?.ToString("N4") ?? "no reg."),
        _            => ("-", "-")
    };
}
