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
    /// ⚠️ DEPRECATED: Obtiene los registros de ventas de la propuesta para un periodo.
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
    public Task<IReadOnlyList<RegistroVenta>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        // ❌ Endpoint INCORRECTO: no documentado en manual v25, retorna 500 en producción
        // Mantenido solo para referencia histórica
        var endpoint = $"/libros/rvie/propuesta/web/registroslibros/{periodo}/cabecera";
        return SendAsync<IReadOnlyList<RegistroVenta>>(HttpMethod.Get, endpoint, null, cancellationToken);
    }

    /// <summary>
    /// Exporta propuesta RVIE y obtiene un ticket para monitorear el procesamiento.
    /// Según manual v25 pág 48, servicio 5.18. Método: GET (parámetros en query string).
    /// Ruta: /libros/rvie/propuesta/web/propuesta/{periodo}/exportapropuesta (rvie, sin codLibro).
    /// Retorna TicketEstado con el número de ticket que puede consultarse con ConsultarTicketAsync.
    /// </summary>
    public Task<TicketEstado> ExportarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Get, SireEndpoints.RvieExportarPropuesta(periodo), null, cancellationToken);

    // Según manual v25 pág 34, servicio 5.8. Ruta: /libros/rvie/propuesta/web/propuesta/{periodo}/aceptapropuesta.
    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RvieAceptar(periodo), null, cancellationToken);

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
        => SendAsync<TicketEstado>(HttpMethod.Post, SireEndpoints.RvieRegistrarPreliminar(periodo), null, cancellationToken);

    public async Task<TicketEstado> ConsultarTicketAsync(string numTicket, string periodo, CancellationToken cancellationToken = default)
    {
        // El servicio 5.16 devuelve un wrapper paginado { paginacion, registros[] }.
        // No se puede deserializar directamente a TicketEstado.
        var respuesta = await SendAsync<TicketConsultaResponse>(
            HttpMethod.Get, SireEndpoints.ConsultarTicket(numTicket, periodo), null, cancellationToken);
        return respuesta.ToTicketEstado();
    }

    /// <summary>
    /// Descarga la constancia de recepción dado el nombre del archivo.
    /// Según manual v25 pág 60, servicio 5.26.
    /// El nomArchivo debe obtenerse previamente del sistema (ej: LE20100096260202212001404000111112.pdf).
    /// </summary>
    public Task<ConstanciaCierre> DescargarConstanciaAsync(string nomArchivo, CancellationToken cancellationToken = default)
        => DescargarConstanciaBaseAsync(SireEndpoints.RvieConstancia(nomArchivo), nomArchivo, cancellationToken);

    /// <summary>
    /// Descarga el archivo de reporte generado por el servicio 5.17 (archivoreporte).
    /// La <paramref name="rutaCompleta"/> debe ser la ruta relativa construida por <see cref="SireEndpoints.DescargarArchivo"/>.
    /// </summary>
    public Task<ConstanciaCierre> DescargarArchivoReporteAsync(string rutaCompleta, string nomArchivo, CancellationToken cancellationToken = default)
        => DescargarConstanciaBaseAsync(rutaCompleta, nomArchivo, cancellationToken);
}
