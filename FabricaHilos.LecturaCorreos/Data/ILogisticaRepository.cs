namespace FabricaHilos.LecturaCorreos.Data;

using FabricaHilos.LecturaCorreos.Models;

public interface ILogisticaRepository
{
    // ── Escritura ────────────────────────────────────────────────────────────

    Task<(long IdGenerado, int CodigoResultado, string MensajeResultado)>
        InsertarDocumentoAsync(DocumentoXml doc);

    Task<(int CodigoResultado, string MensajeResultado)>
        InsertarLineaAsync(long documentoId, LineaDocumento linea);

    Task<(int CodigoResultado, string MensajeResultado)>
        InsertarCuotaAsync(long documentoId, CuotaPago cuota);

    Task<(int CodigoResultado, string MensajeResultado)>
        RegistrarErrorAsync(string nombreArchivo, string extension, string cuentaCorreo,
                            string asunto, string remitente, string tipoError,
                            string mensajeError, string stackTrace, string contenidoAdjunto);

    Task<(long IdGenerado, int CodigoResultado, string MensajeResultado)>
        GuardarAdjuntoPdfAsync(AdjuntoPdf pdf);

    Task<(int CodigoResultado, string MensajeResultado)>
        MarcarErrorRevisadoAsync(long id, string observaciones);

    // ── Consultas ────────────────────────────────────────────────────────────

    Task<IReadOnlyList<DocumentoResumen>>
        ListarDocumentosAsync(FiltroDocumentos filtro);

    /// <summary>
    /// Devuelve la cabecera, las líneas y las cuotas de un documento.
    /// </summary>
    Task<(DocumentoResumen? Cabecera, IReadOnlyList<LineaDocumento> Lineas, IReadOnlyList<CuotaPago> Cuotas)>
        ObtenerDocumentoAsync(long id);

    Task<IReadOnlyList<DocumentoPorVencer>>
        ObtenerDocumentosPorVencerAsync(int diasAdelante = 30);

    Task<IReadOnlyList<ErrorProcesamiento>>
        ListarErroresAsync(char procesado = 'N', DateTime? fechaDesde = null);

    Task<IReadOnlyList<ResumenPorCuenta>>
        ObtenerResumenPorCuentaAsync(DateTime fechaDesde, DateTime fechaHasta);

    Task<IReadOnlyList<ResumenPorProveedor>>
        ObtenerResumenPorProveedorAsync(DateTime fechaDesde, DateTime fechaHasta);

    Task<IReadOnlyList<GuiaPorTransportista>>
        ObtenerGuiasPorTransportistaAsync(string rucTransportista, DateTime? fechaDesde = null);

    Task<IReadOnlyList<DocumentoPorPedido>>
        BuscarPorPedidoAsync(string numeroPedido);
}
