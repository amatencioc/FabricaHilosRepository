using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public sealed class SireComprasService : SireServiceBase, ISireComprasService
{
    protected override string LibroNombre => "RCE";

    public SireComprasService(HttpClient httpClient, ISireAuthService authService, IOptions<SireOptions> options)
        : base(httpClient, authService, options) { }

    public async Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
    {
        var ejercicios = await SendAsync<IReadOnlyList<EjercicioPeriodosDto>>(
            HttpMethod.Get, SireEndpoints.RcePeriodos, null, cancellationToken);

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
    /// ⚠️ DEPRECATED: Obtiene los registros de compras de la propuesta para un periodo.
    /// El endpoint original (/registroslibros/{periodo}/cabecera) no está documentado en manual v25
    /// y retorna HTTP 500 en producción.
    /// 
    /// USO CORRECTO: Para obtener registros, use el flujo:
    /// 1. ExportarPropuestaAsync(periodo) → obtiene TicketEstado
    /// 2. TicketPollingHelper.EsperarEstadoFinalAsync() → espera procesamiento
    /// 3. DescargarConstanciaAsync(nomArchivo) → descarga archivo ZIP resultante
    /// 4. Descomprimir y procesar archivo plano con registros
    /// </summary>
    [Obsolete("Endpoint no documentado en SUNAT manual v25. Use ExportarPropuestaAsync() + TicketPollingHelper + DescargarConstanciaAsync() en su lugar. Este método retorna HTTP 500.", false)]
    public Task<IReadOnlyList<RegistroCompra>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        // ❌ Endpoint INCORRECTO: no documentado en manual v25, retorna 500 en producción
        // Mantenido solo para referencia histórica
        var endpoint = $"/libros/rce/propuesta/web/registroslibros/{periodo}/cabecera";
        return SendAsync<IReadOnlyList<RegistroCompra>>(HttpMethod.Get, endpoint, null, cancellationToken);
    }

    /// <summary>
    /// Exporta propuesta RCE y obtiene un ticket para monitorear el procesamiento.
    /// Patrón equivalente a RVIE (manual v25 pág 48, servicio 5.18). Método: GET (parámetros en query string).
    /// Retorna TicketEstado con el número de ticket que puede consultarse con ConsultarTicketAsync.
    /// </summary>
    public Task<TicketEstado> ExportarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Get, SireEndpoints.RceExportarPropuesta(periodo), null, cancellationToken);

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RceAceptar(periodo), null, cancellationToken);

    /// <remarks>
    /// En el servicio real el reemplazo se realiza vía TUS (ITusUploadService).
    /// Este método queda como fallback multipart para compatibilidad.
    /// El controlador invoca directamente <see cref="ITusUploadService.ReemplazarPropuestaRceAsync"/>.
    /// </remarks>
    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => SendMultipartAsync(SireEndpoints.TusUploadPath, contenidoArchivo, nombreArchivo, cancellationToken);

    /// <summary>
    /// Registra el preliminar del periodo (equivalente al antiguo "cerrar").
    /// Patrón equivalente a RVIE (manual v25 pág 35, servicio 5.9).
    /// </summary>
    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RceRegistrarPreliminar(periodo), null, cancellationToken);

    public Task<TicketEstado> ConsultarTicketAsync(string numTicket, string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Get, SireEndpoints.ConsultarTicket(numTicket, periodo), null, cancellationToken);

    /// <summary>
    /// Descarga la constancia de recepción dado el nombre del archivo.
    /// Patrón equivalente a RVIE (manual v25 pág 60, servicio 5.26).
    /// El nomArchivo debe obtenerse previamente del sistema (ej: LE20100096260202212000800000111112.pdf).
    /// </summary>
    public Task<ConstanciaCierre> DescargarConstanciaAsync(string nomArchivo, CancellationToken cancellationToken = default)
        => DescargarConstanciaBaseAsync(SireEndpoints.RceConstancia(nomArchivo), nomArchivo, cancellationToken);
}
