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

    /// <summary>Registra en <c>FH_LECTCORREOS_ARCHIVOS</c> un archivo confirmado en disco.</summary>
    Task RegistrarArchivoAsync(long? documentoId, string tipoArchivo, string nombreOriginal, string nombreGuardado, string rutaArchivo);

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

    /// <summary>
    /// Devuelve el <c>DOC_ID</c> de un documento ya existente en <c>FH_LC_DOCUMENTO</c>
    /// buscando por <paramref name="numeroDocumento"/>. Retorna <c>null</c> si no existe.
    /// </summary>
    Task<long?> ObtenerDocumentoIdAsync(string numeroDocumento);

    /// <summary>
    /// Devuelve el <c>ID</c> de un documento existente buscando por RUC emisor, serie y
    /// correlativo (comparado numéricamente para ignorar ceros de relleno).
    /// Útil para rescatar PDFs huérfanos cuyo XML llegó en un correo separado.
    /// </summary>
    Task<long?> ObtenerDocumentoIdPorRucYSerieAsync(string rucEmisor, string serie, long correlativo);

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
