namespace FabricaHilos.Services.Sistemas;

// ── DTOs ────────────────────────────────────────────────────────────────────

public class AnularDocumentoResultDto
{
    /// <summary>true si el documento existe en DOCUVENT.</summary>
    public bool ExisteDocumento       { get; set; }

    /// <summary>true si se encontró fila en MOVGLOS.</summary>
    public bool ExisteMovGlos         { get; set; }

    /// <summary>true si se encontró fila en NRODOC.</summary>
    public bool ExisteNroDoc          { get; set; }

    /// <summary>true si se encontró fila en NROLIBR.</summary>
    public bool ExisteNroLibr         { get; set; }

    // ── Datos de DOCUVENT ─────────────────────────────────────────────────────
    /// <summary>ESTADO en DOCUVENT: 0 = Emitido, 9 = Anulado.</summary>
    public string? EstadoDocumento { get; set; }
    public string  EstadoDocumentoLabel => EstadoDocumento == "9" ? "Anulado" : EstadoDocumento == "0" ? "Emitido" : EstadoDocumento ?? "";

    // ── Datos de MOVGLOS ──────────────────────────────────────────────────────
    public string? Ano     { get; set; }
    public string? Mes     { get; set; }
    public string? Libro   { get; set; }
    public string? Voucher { get; set; }
    /// <summary>ESTADO en MOVGLOS: 0 = Emitido, 9 = Anulado.</summary>
    public string? EstadoMovGlos { get; set; }
    public string  EstadoMovGlosLabel => EstadoMovGlos == "9" ? "Anulado" : EstadoMovGlos == "0" ? "Emitido" : EstadoMovGlos ?? "";

    // ── Datos de NRODOC ───────────────────────────────────────────────────────
    public string? NroDoc  { get; set; }

    // ── Datos de NROLIBR ─────────────────────────────────────────────────────
    public string? NroLibr { get; set; }

    /// <summary>Mensaje de error si ocurre una excepción.</summary>
    public string? Error   { get; set; }
}

public class RestablecerResultDto
{
    public bool    Ok      { get; set; }
    public int     Filas   { get; set; }
    public string? Error   { get; set; }
}

public class RestablecerPasoDto
{
    public bool    Ok      { get; set; }
    public int     Filas   { get; set; }
    public string? Mensaje { get; set; }
    public string? Error   { get; set; }
}
