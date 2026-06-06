using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public sealed class SireVentasService : SireServiceBase, ISireVentasService
{
    protected override string LibroNombre => "RVIE";

    public SireVentasService(HttpClient httpClient, ISireAuthService authService, IOptions<SireOptions> options)
        : base(httpClient, authService, options) { }

    public async Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
    {
        // Deserializar estructura anidada de SUNAT y aplanar a PropuestaDto
        var ejercicios = await SendAsync<IReadOnlyList<EjercicioPeriodosDto>>(
            HttpMethod.Get, SireEndpoints.RviePeriodos, null, cancellationToken);

        var resultado = new List<PropuestaDto>();
        foreach (var ejercicio in ejercicios)
        {
            foreach (var periodo in ejercicio.ListaPeriodos)
            {
                resultado.Add(new PropuestaDto
                {
                    Periodo = periodo.PerTributario,
                    Descripcion = periodo.DesEstado,
                    Estado = periodo.CodEstado
                });
            }
        }
        return resultado;
    }

    /// <summary>
    /// Obtiene los registros de ventas de la propuesta para un periodo.
    /// NOTA: El manual v25 no documenta un endpoint GET directo de "cabecera".
    /// Este método se mantiene por compatibilidad pero probablemente falle.
    /// Considerar usar ExportarPropuestaAsync + descargar archivo en su lugar.
    /// </summary>
    public Task<IReadOnlyList<RegistroVenta>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        // Endpoint no documentado en manual v25, mantenido por compatibilidad
        var endpoint = $"/libros/rvie/propuesta/web/registroslibros/{periodo}/cabecera";
        return SendAsync<IReadOnlyList<RegistroVenta>>(HttpMethod.Get, endpoint, null, cancellationToken);
    }

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RvieAceptar(periodo), new { }, cancellationToken);

    /// <remarks>
    /// En el servicio real el reemplazo se realiza vía TUS (ITusUploadService).
    /// Este método queda como fallback multipart para compatibilidad.
    /// El controlador invoca directamente <see cref="ITusUploadService.ReemplazarPropuestaRvieAsync"/>.
    /// </remarks>
    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => SendMultipartAsync(SireEndpoints.TusUploadPath, contenidoArchivo, nombreArchivo, cancellationToken);

    /// <summary>
    /// Registra el preliminar del periodo (equivalente al antiguo "cerrar").
    /// Según manual v25 pág 35, servicio 5.9.
    /// </summary>
    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RvieRegistrarPreliminar(periodo), new { }, cancellationToken);

    public Task<TicketEstado> ConsultarTicketAsync(string numTicket, string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Get, SireEndpoints.ConsultarTicket(numTicket, periodo), null, cancellationToken);

    /// <summary>
    /// Descarga la constancia de recepción dado el nombre del archivo.
    /// Según manual v25 pág 60, servicio 5.26.
    /// El nomArchivo debe obtenerse previamente del sistema (ej: LE20100096260202212001404000111112.pdf).
    /// </summary>
    public Task<ConstanciaCierre> DescargarConstanciaAsync(string nomArchivo, CancellationToken cancellationToken = default)
        => DescargarConstanciaBaseAsync(SireEndpoints.RvieConstancia(nomArchivo), nomArchivo, cancellationToken);
}
