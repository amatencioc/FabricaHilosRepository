namespace FabricaHilos.Models.Sire;

/// <summary>
/// Entrada del padrón SUNAT de Sujetos Sin Capacidad Operativa (SSCO).
/// Fuente: ssco/sujesincapacidadOperativa.xlsx — descarga mensual.
/// </summary>
public sealed class SscoListaEntry
{
    public string    Ruc             { get; init; } = string.Empty;
    public string?   RazonSocial     { get; init; }
    public string?   ResolucionAtrib { get; init; }
    public DateTime? FchResolucion   { get; init; }

    /// <summary>
    /// Fecha en la que la resolución quedó firme.
    /// Un comprobante con F_EMISION >= FchQuedoFirme es inválido para el fisco.
    /// </summary>
    public DateTime  FchQuedoFirme   { get; init; }
    public DateTime? FchPublicacion  { get; init; }
    public string?   DocRepLegal     { get; init; }
    public string?   NomRepLegal     { get; init; }
    public DateTime? FchCarga        { get; init; }
    public int?      PeriodoCarga    { get; init; }
}
