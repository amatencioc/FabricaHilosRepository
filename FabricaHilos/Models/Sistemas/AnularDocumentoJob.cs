namespace FabricaHilos.Models.Sistemas;

// ── Estado de un paso individual ─────────────────────────────────────────────
public class AnularDocumentoPasoEstado
{
    public int     Numero  { get; set; }
    public string  Estado  { get; set; } = "pending"; // pending | running | ok | error
    public string? Mensaje { get; set; }
    public string? Error   { get; set; }
    public int     Filas   { get; set; }
}

// ── Job completo (Restablecer o Revertir) ─────────────────────────────────────
public class AnularDocumentoJob
{
    public string   JobId      { get; init; } = Guid.NewGuid().ToString("N")[..10];
    public string   Tipo       { get; init; } = "restablecer"; // restablecer | revertir
    public string   Estado     { get; set;  } = "running";     // running | done | aborted
    public string?  Error      { get; set;  }
    public DateTime CreadoEn   { get; init; } = DateTime.UtcNow;
    public DateTime? FinalizadoEn { get; set; }

    public List<AnularDocumentoPasoEstado> Pasos { get; init; } = [];

    // ── Parámetros capturados desde la sesión ──────────────────────────────────
    public string ConnString   { get; init; } = "";
    public string Schema       { get; init; } = "";

    // ── Parámetros de la operación ─────────────────────────────────────────────
    public string TipoDoc         { get; init; } = "";
    public string Serie           { get; init; } = "";
    public string Numero          { get; init; } = "";   // número original buscado
    public string NumeroBusqueda  { get; init; } = "";   // número - 1
    public string VoucherBusqueda { get; init; } = "";
    public string Ano             { get; init; } = "";
    public string Mes             { get; init; } = "";
    public string Libro           { get; init; } = "";

    // Revertir
    public string NumeroAnterior  { get; init; } = "";
    public string VoucherAnterior { get; init; } = "";
}
