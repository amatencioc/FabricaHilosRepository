using System.Text.Json.Serialization;

namespace FabricaHilos.Sire.Models;

/// <summary>
/// Objeto de paginación devuelto por el servicio 5.16 consultaestadotickets.
/// </summary>
public sealed class TicketConsultaPaginacion
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("perPage")]
    public int PerPage { get; set; }

    [JsonPropertyName("totalRegistros")]
    public int TotalRegistros { get; set; }
}

/// <summary>
/// Detalle interno del ticket dentro de registros[].detalleTicket.
/// Según manual SIRE Ventas v25 pág 44-45, servicio 5.16.
/// </summary>
public sealed class DetalleTicketDto
{
    [JsonPropertyName("numTicket")]
    public string? NumTicket { get; set; }

    [JsonPropertyName("codEstadoEnvio")]
    public string? CodEstadoEnvio { get; set; }

    [JsonPropertyName("desEstadoEnvio")]
    public string? DesEstadoEnvio { get; set; }

    /// <summary>Nombre del archivo de reporte (disponible cuando el proceso terminó).</summary>
    [JsonPropertyName("nomArchivoReporte")]
    public string? NomArchivoReporte { get; set; }

    [JsonPropertyName("fecCargaImportacion")]
    public string? FecCargaImportacion { get; set; }

    [JsonPropertyName("horaCargaImportacion")]
    public string? HoraCargaImportacion { get; set; }

    [JsonPropertyName("cntFilasvalidada")]
    public int? CntFilasValidada { get; set; }

    [JsonPropertyName("cntCPError")]
    public int? CntCPError { get; set; }

    [JsonPropertyName("cntCPInformados")]
    public int? CntCPInformados { get; set; }
}

/// <summary>
/// Elemento de archivoReporte[] dentro de registros[].
/// Según manual SIRE Ventas v25 pág 45, servicio 5.16.
/// </summary>
public sealed class ArchivoReporteItemDto
{
    /// <summary>Código del tipo de archivo de reporte (ej: "01", "ZIP"). Puede ser null.</summary>
    [JsonPropertyName("codTipoAchivoReporte")]
    public string? CodTipoArchivoReporte { get; set; }

    /// <summary>Nombre del archivo de reporte generado (ej: PP20100096260202601140000001111110.ZIP).</summary>
    [JsonPropertyName("nomArchivoReporte")]
    public string? NomArchivoReporte { get; set; }

    /// <summary>Nombre del archivo contenido.</summary>
    [JsonPropertyName("nomArchivoContenido")]
    public string? NomArchivoContenido { get; set; }
}

/// <summary>
/// Un registro dentro del array registros[] de la respuesta del servicio 5.16.
/// Según manual SIRE Ventas v25 pág 43-45.
/// </summary>
public sealed class TicketRegistroDto
{
    [JsonPropertyName("numTicket")]
    public string? NumTicket { get; set; }

    [JsonPropertyName("perTributario")]
    public string? PerTributario { get; set; }

    [JsonPropertyName("fecCargaImportacion")]
    public string? FecCargaImportacion { get; set; }

    [JsonPropertyName("fecInicioProceso")]
    public string? FecInicioProceso { get; set; }

    /// <summary>Código del indicador de carga masiva. Ver Anexo I del manual.</summary>
    [JsonPropertyName("codProceso")]
    public string? CodProceso { get; set; }

    [JsonPropertyName("desProceso")]
    public string? DesProceso { get; set; }

    /// <summary>
    /// Código de estado de envío. Ver Anexo III del manual.
    /// 3 = completado, 4 = completado con errores.
    /// </summary>
    [JsonPropertyName("codEstadoProceso")]
    public string? CodEstadoProceso { get; set; }

    /// <summary>Descripción del estado: EN_PROCESO, COMPLETADO, ERROR, RECHAZADO.</summary>
    [JsonPropertyName("desEstadoProceso")]
    public string? DesEstadoProceso { get; set; }

    [JsonPropertyName("nomArchivoImportacion")]
    public string? NomArchivoImportacion { get; set; }

    [JsonPropertyName("showReportesDescarga")]
    public int? ShowReportesDescarga { get; set; }

    [JsonPropertyName("detalleTicket")]
    public DetalleTicketDto? DetalleTicket { get; set; }

    [JsonPropertyName("archivoReporte")]
    public List<ArchivoReporteItemDto>? ArchivoReporte { get; set; }
}

/// <summary>
/// Respuesta paginada completa del servicio 5.16 consultaestadotickets.
/// Según manual SIRE Ventas v25 pág 43-45.
/// </summary>
public sealed class TicketConsultaResponse
{
    [JsonPropertyName("paginacion")]
    public TicketConsultaPaginacion? Paginacion { get; set; }

    [JsonPropertyName("registros")]
    public List<TicketRegistroDto>? Registros { get; set; }

    /// <summary>
    /// Mapea el primer registro a un <see cref="TicketEstado"/> normalizado.
    /// Devuelve un TicketEstado vacío si no hay registros.
    /// </summary>
    public TicketEstado ToTicketEstado()
    {
        var reg = Registros?.FirstOrDefault();
        if (reg is null)
            return new TicketEstado();

        var archivo = reg.ArchivoReporte?.FirstOrDefault();
        // nomArchivoReporte también puede estar en detalleTicket (fallback)
        var nomArchivo = archivo?.NomArchivoReporte ?? reg.DetalleTicket?.NomArchivoReporte;
        var codTipoArchivo = archivo?.CodTipoArchivoReporte;

        return new TicketEstado
        {
            NumTicket            = reg.NumTicket ?? string.Empty,
            Estado               = reg.DesEstadoProceso ?? string.Empty,
            CodEstadoProceso     = reg.CodEstadoProceso ?? string.Empty,
            CodProceso           = reg.CodProceso ?? string.Empty,
            PerTributario        = reg.PerTributario ?? string.Empty,
            NomArchivoImportacion = reg.NomArchivoImportacion,
            Mensaje              = reg.DetalleTicket?.DesEstadoEnvio ?? string.Empty,
            ArchivoReporte       = nomArchivo is not null
                ? new ArchivoReporteDto
                {
                    NomArchivoReporte    = nomArchivo,
                    CodTipoArchivoReporte = codTipoArchivo
                }
                : null
        };
    }
}
