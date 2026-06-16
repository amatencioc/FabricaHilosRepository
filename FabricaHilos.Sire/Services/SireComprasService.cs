using System.Text.Json;
using FabricaHilos.Sire.Constants;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;
using FabricaHilos.Sire.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FabricaHilos.Sire.Services;

public sealed class SireComprasService : SireServiceBase, ISireComprasService
{
    protected override string LibroNombre => "RCE";

    private readonly ILogger<SireComprasService> _logger;

    public SireComprasService(HttpClient httpClient, ISireAuthService authService, IOptions<SireOptions> options, ILogger<SireComprasService> logger)
        : base(httpClient, authService, options)
    {
        _logger = logger;
    }

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
    /// Según manual Compras v22 pág 84, servicio 5.34. Método: GET.
    /// Ruta: /libros/rce/propuesta/web/propuesta/{periodo}/exportacioncomprobantepropuesta (distinta al RVIE).
    /// Retorna TicketEstado con el número de ticket que puede consultarse con ConsultarTicketAsync.
    /// </summary>
    public Task<TicketEstado> ExportarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Get, SireEndpoints.RceExportarPropuesta(periodo), null, cancellationToken);

    // Según manual Compras v22 pág 40, servicio 5.2. Ruta: /libros/rce/propuesta/web/registroslibros/{periodo}/aceptarpropuesta.
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
    /// Registra el preliminar del periodo RCE.
    /// Según manual Compras v22 pág 42, servicio 5.4.
    /// Ruta distinta al RVIE: /libros/rce/preliminar/web/registroslibros/{periodo}/registrapreliminares (plural).
    /// </summary>
    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RceRegistrarPreliminar(periodo), null, cancellationToken);

    public async Task<TicketEstado> ConsultarTicketAsync(string numTicket, string periodo, CancellationToken cancellationToken = default)
    {
        // El servicio 5.16 devuelve un wrapper paginado { paginacion, registros[] }.
        // Obtenemos el raw JSON para diagnóstico antes de deserializar.
        var rawJson = await SendAsync<string>(
            HttpMethod.Get, SireEndpoints.ConsultarTicket(numTicket, periodo), null, cancellationToken);
        _logger.LogDebug("[SIRE-RCE] Ticket {Ticket} raw: {Json}", numTicket, rawJson);

        var respuesta = JsonSerializer.Deserialize<TicketConsultaResponse>(rawJson, JsonOptions) ?? new TicketConsultaResponse();
        var estado = respuesta.ToTicketEstado();

        if (estado.EsFinal && estado.ArchivoReporte?.NomArchivoReporte is null)
            _logger.LogWarning("[SIRE-RCE] Ticket {Ticket} finalizado ({Estado}) sin archivoReporte. Raw: {Json}", numTicket, estado.Estado, rawJson);

        return estado;
    }

    /// <summary>
    /// Descarga la constancia de recepción dado el nombre del archivo.
    /// Según manual Compras v22 pág 107, servicio 5.49.
    /// Ruta distinta al RVIE: constanciarecepcion con parámetro nomConstanciaRecepcion.
    /// El nomConstanciaRecepcion debe obtenerse previamente del sistema (ej: LE20100096260202212000800000111112.pdf).
    /// </summary>
    public Task<ConstanciaCierre> DescargarConstanciaAsync(string nomArchivo, CancellationToken cancellationToken = default)
        => DescargarConstanciaBaseAsync(SireEndpoints.RceConstancia(nomArchivo), nomArchivo, cancellationToken);

    /// <summary>
    /// Descarga el archivo de reporte generado por el servicio 5.17 (archivoreporte).
    /// La <paramref name="rutaCompleta"/> debe ser la ruta relativa construida por <see cref="SireEndpoints.DescargarArchivo"/>.
    /// </summary>
    public Task<ConstanciaCierre> DescargarArchivoReporteAsync(string rutaCompleta, string nomArchivo, CancellationToken cancellationToken = default)
        => DescargarConstanciaBaseAsync(rutaCompleta, nomArchivo, cancellationToken);
}
