namespace FabricaHilos.LecturaCorreos.Data;

using FabricaHilos.LecturaCorreos.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

public class LogisticaRepository : ILogisticaRepository
{
    private readonly string _connStr;
    private readonly ILogger<LogisticaRepository> _logger;

    public LogisticaRepository(IConfiguration configuration, ILogger<LogisticaRepository> logger)
    {
        _connStr = configuration.GetConnectionString("LaColonialConnection")
                   ?? throw new InvalidOperationException("ConnectionStrings:LaColonialConnection no configurada.");
        _logger = logger;
    }

    // ── SP_INSERTAR_DOCUMENTO ─────────────────────────────────────────────────
    public Task<(long IdGenerado, int CodigoResultado, string MensajeResultado)>
        InsertarDocumentoAsync(DocumentoXml doc)
        => OracleRetry.EjecutarAsync(() => InsertarDocumentoAsyncCore(doc), _logger, nameof(InsertarDocumentoAsync));

    private async Task<(long IdGenerado, int CodigoResultado, string MensajeResultado)>
        InsertarDocumentoAsyncCore(DocumentoXml doc)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = "PKG_LC_LOGISTICA.SP_INSERTAR_DOCUMENTO";
        cmd.CommandType    = System.Data.CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = 60;

        // ── IN ────────────────────────────────────────────────────────────────
        AddParam(cmd, "P_NOMBRE_ARCHIVO",          OracleDbType.Varchar2,  doc.NombreArchivo);
        AddParam(cmd, "P_CUENTA_CORREO",           OracleDbType.Varchar2,  doc.CuentaCorreo);
        AddParam(cmd, "P_ASUNTO_CORREO",           OracleDbType.Varchar2,  doc.AsuntoCorreo);
        AddParam(cmd, "P_REMITENTE_CORREO",        OracleDbType.Varchar2,  doc.RemitenteCorreo);
        AddParam(cmd, "P_FECHA_CORREO",            OracleDbType.Date,      doc.FechaCorreo as object ?? DBNull.Value);
        AddParam(cmd, "P_TIPO_XML",                OracleDbType.Varchar2,  doc.TipoXml);
        AddParam(cmd, "P_TIPO_DOCUMENTO",          OracleDbType.Varchar2,  doc.TipoDocumento);
        AddParam(cmd, "P_SERIE",                   OracleDbType.Varchar2,  doc.Serie);
        AddParam(cmd, "P_CORRELATIVO",             OracleDbType.Varchar2,  doc.Correlativo);
        AddParam(cmd, "P_NUMERO_DOCUMENTO",        OracleDbType.Varchar2,  doc.NumeroDocumento);
        AddParam(cmd, "P_FECHA_EMISION",           OracleDbType.Date,      doc.FechaEmision as object ?? DBNull.Value);
        AddParam(cmd, "P_HORA_EMISION",            OracleDbType.Varchar2,  doc.HoraEmision);
        AddParam(cmd, "P_FECHA_VENCIMIENTO",       OracleDbType.Date,      doc.FechaVencimiento as object ?? DBNull.Value);
        AddParam(cmd, "P_RUC_EMISOR",              OracleDbType.Varchar2,  doc.RucEmisor);
        AddParam(cmd, "P_NOMBRE_COMERCIAL_EMISOR", OracleDbType.Varchar2,  doc.NombreComercialEmisor);
        AddParam(cmd, "P_RAZON_SOCIAL_EMISOR",     OracleDbType.Varchar2,  doc.RazonSocialEmisor);
        AddParam(cmd, "P_DIRECCION_EMISOR",        OracleDbType.Varchar2,  doc.DireccionEmisor);
        AddParam(cmd, "P_UBIGEO_EMISOR",           OracleDbType.Varchar2,  doc.UbigeoEmisor);
        AddParam(cmd, "P_RUC_RECEPTOR",            OracleDbType.Varchar2,  doc.RucReceptor);
        AddParam(cmd, "P_RAZON_SOCIAL_RECEPTOR",   OracleDbType.Varchar2,  doc.RazonSocialReceptor);
        AddParam(cmd, "P_DIRECCION_RECEPTOR",      OracleDbType.Varchar2,  doc.DireccionReceptor);
        AddParam(cmd, "P_UBIGEO_RECEPTOR",         OracleDbType.Varchar2,  doc.UbigeoReceptor);
        AddParam(cmd, "P_MONEDA",                  OracleDbType.Varchar2,  doc.Moneda);
        AddParam(cmd, "P_BASE_IMPONIBLE",          OracleDbType.Decimal,   doc.BaseImponible);
        AddParam(cmd, "P_TOTAL_IGV",               OracleDbType.Decimal,   doc.TotalIgv);
        AddParam(cmd, "P_TOTAL_EXONERADO",         OracleDbType.Decimal,   doc.TotalExonerado);
        AddParam(cmd, "P_TOTAL_INAFECTO",          OracleDbType.Decimal,   doc.TotalInafecto);
        AddParam(cmd, "P_TOTAL_GRATUITO",          OracleDbType.Decimal,   doc.TotalGratuito);
        AddParam(cmd, "P_TOTAL_DESCUENTO",         OracleDbType.Decimal,   doc.TotalDescuento);
        AddParam(cmd, "P_TOTAL_CARGO",             OracleDbType.Decimal,   doc.TotalCargo);
        AddParam(cmd, "P_TOTAL_ANTICIPOS",         OracleDbType.Decimal,   doc.TotalAnticipos);
        AddParam(cmd, "P_TOTAL_PAGAR",             OracleDbType.Decimal,   doc.TotalPagar);
        AddParam(cmd, "P_FORMA_PAGO",              OracleDbType.Varchar2,  doc.FormaPago);
        AddParam(cmd, "P_MONTO_NETO_PENDIENTE",    OracleDbType.Decimal,   doc.MontoNetoPendiente);
        AddParam(cmd, "P_TIENE_DETRACCION",        OracleDbType.Char,      doc.TieneDetraccion ? "S" : "N");
        AddParam(cmd, "P_COD_BIEN_DETRACCION",     OracleDbType.Varchar2,  doc.CodBienDetraccion);
        AddParam(cmd, "P_NRO_CTA_DETRACCION",      OracleDbType.Varchar2,  doc.NroCuentaDetraccion);
        AddParam(cmd, "P_PCT_DETRACCION",          OracleDbType.Decimal,   doc.PctDetraccion);
        AddParam(cmd, "P_MONTO_DETRACCION",        OracleDbType.Decimal,   doc.MontoDetraccion);
        AddParam(cmd, "P_NUMERO_PEDIDO",           OracleDbType.Varchar2,  doc.NumeroPedido);
        AddParam(cmd, "P_NUMERO_GUIA",             OracleDbType.Varchar2,  doc.NumeroGuia);
        AddParam(cmd, "P_NUMERO_DOC_REF",          OracleDbType.Varchar2,  doc.NumeroDocRef);
        AddParam(cmd, "P_MODALIDAD_TRASLADO",      OracleDbType.Varchar2,  doc.ModalidadTraslado);
        AddParam(cmd, "P_MOTIVO_TRASLADO",         OracleDbType.Varchar2,  doc.MotivoTraslado);
        AddParam(cmd, "P_MODO_TRANSPORTE",         OracleDbType.Varchar2,  doc.ModoTransporte);
        AddParam(cmd, "P_PESO_BRUTO",              OracleDbType.Decimal,   doc.PesoBruto);
        AddParam(cmd, "P_UNIDAD_PESO",             OracleDbType.Varchar2,  doc.UnidadPeso);
        AddParam(cmd, "P_FECHA_INICIO_TRASLADO",   OracleDbType.Date,      doc.FechaInicioTraslado as object ?? DBNull.Value);
        AddParam(cmd, "P_FECHA_FIN_TRASLADO",      OracleDbType.Date,      doc.FechaFinTraslado as object ?? DBNull.Value);
        AddParam(cmd, "P_RUC_TRANSPORTISTA",       OracleDbType.Varchar2,  doc.RucTransportista);
        AddParam(cmd, "P_RAZON_SOC_TRANSPORTISTA", OracleDbType.Varchar2,  doc.RazonSocTransportista);
        AddParam(cmd, "P_NOMBRE_CONDUCTOR",        OracleDbType.Varchar2,  doc.NombreConductor);
        AddParam(cmd, "P_LICENCIA_CONDUCTOR",      OracleDbType.Varchar2,  doc.LicenciaConductor);
        AddParam(cmd, "P_PLACA_VEHICULO",          OracleDbType.Varchar2,  doc.PlacaVehiculo);
        AddParam(cmd, "P_MARCA_VEHICULO",          OracleDbType.Varchar2,  doc.MarcaVehiculo);
        AddParam(cmd, "P_NRO_DOC_CONDUCTOR",       OracleDbType.Varchar2,  doc.NroDocConductor);
        AddParam(cmd, "P_UBIGEO_ORIGEN",           OracleDbType.Varchar2,  doc.UbigeoOrigen);
        AddParam(cmd, "P_DIR_ORIGEN",              OracleDbType.Varchar2,  doc.DirOrigen);
        AddParam(cmd, "P_UBIGEO_DESTINO",          OracleDbType.Varchar2,  doc.UbigeoDestino);
        AddParam(cmd, "P_DIR_DESTINO",             OracleDbType.Varchar2,  doc.DirDestino);
        AddParam(cmd, "P_VENDEDOR",                OracleDbType.Varchar2,  doc.Vendedor);

        var pXml = new OracleParameter("P_XML_CONTENIDO", OracleDbType.Clob)
        {
            Direction = System.Data.ParameterDirection.Input,
            Value     = string.IsNullOrEmpty(doc.XmlContenido) ? DBNull.Value : (object)doc.XmlContenido,
        };
        cmd.Parameters.Add(pXml);

        // ── OUT ───────────────────────────────────────────────────────────────
        var pId  = new OracleParameter("P_ID_GENERADO",      OracleDbType.Decimal)
                   { Direction = System.Data.ParameterDirection.Output };
        var pCod = new OracleParameter("P_CODIGO_RESULTADO", OracleDbType.Int32)
                   { Direction = System.Data.ParameterDirection.Output };
        var pMsg = new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 4000)
                   { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCod);
        cmd.Parameters.Add(pMsg);

        await cmd.ExecuteNonQueryAsync();

        long   idGen   = pId.Value  is OracleDecimal od ? (long)od.Value  : 0;
        int    codigo  = pCod.Value is OracleDecimal oc ? (int)oc.Value   : -1;
        string mensaje = pMsg.Value?.ToString() ?? string.Empty;

        return (idGen, codigo, mensaje);
    }

    // ── SP_INSERTAR_LINEA ─────────────────────────────────────────────────────
    public Task<(int CodigoResultado, string MensajeResultado)>
        InsertarLineaAsync(long documentoId, LineaDocumento linea)
        => OracleRetry.EjecutarAsync(() => InsertarLineaAsyncCore(documentoId, linea), _logger, nameof(InsertarLineaAsync));

    private async Task<(int CodigoResultado, string MensajeResultado)>
        InsertarLineaAsyncCore(long documentoId, LineaDocumento linea)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = "PKG_LC_LOGISTICA.SP_INSERTAR_LINEA";
        cmd.CommandType    = System.Data.CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = 60;

        AddParam(cmd, "P_DOCUMENTO_ID",  OracleDbType.Decimal,  documentoId);
        AddParam(cmd, "P_NUMERO_LINEA",  OracleDbType.Int32,    linea.NumeroLinea);
        AddParam(cmd, "P_DESCRIPCION",   OracleDbType.Varchar2, linea.Descripcion);
        AddParam(cmd, "P_NOMBRE_ITEM",   OracleDbType.Varchar2, linea.NombreItem);
        AddParam(cmd, "P_CODIGO_PRODUCTO", OracleDbType.Varchar2, linea.CodigoProducto);
        AddParam(cmd, "P_CODIGO_UNSPSC", OracleDbType.Varchar2, linea.CodigoUNSPSC);
        AddParam(cmd, "P_CANTIDAD",      OracleDbType.Decimal,  linea.Cantidad);
        AddParam(cmd, "P_UNIDAD_MEDIDA", OracleDbType.Varchar2, linea.UnidadMedida);
        AddParam(cmd, "P_PRECIO_UNITARIO", OracleDbType.Decimal, linea.PrecioUnitario);
        AddParam(cmd, "P_PRECIO_CON_IGV", OracleDbType.Decimal, linea.PrecioConIgv);
        AddParam(cmd, "P_ES_GRATUITO",   OracleDbType.Char,     linea.EsGratuito ? "S" : "N");
        AddParam(cmd, "P_SUB_TOTAL",     OracleDbType.Decimal,  linea.SubTotal);
        AddParam(cmd, "P_IGV",           OracleDbType.Decimal,  linea.Igv);
        AddParam(cmd, "P_TOTAL_LINEA",   OracleDbType.Decimal,  linea.TotalLinea);
        AddParam(cmd, "P_AFECTACION_IGV", OracleDbType.Varchar2, linea.AfectacionIgv);
        AddParam(cmd, "P_PORCENTAJE_IGV", OracleDbType.Decimal, linea.PorcentajeIgv);
        AddParam(cmd, "P_LOTE",          OracleDbType.Varchar2, linea.Lote);
        AddParam(cmd, "P_FECHA_VENC_LOTE", OracleDbType.Date,  linea.FechaVencLote as object ?? DBNull.Value);

        var pCod = new OracleParameter("P_CODIGO_RESULTADO",  OracleDbType.Int32)
                   { Direction = System.Data.ParameterDirection.Output };
        var pMsg = new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 4000)
                   { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCod);
        cmd.Parameters.Add(pMsg);

        await cmd.ExecuteNonQueryAsync();

        int    codigo  = pCod.Value is OracleDecimal oc ? (int)oc.Value   : -1;
        string mensaje = pMsg.Value?.ToString() ?? string.Empty;
        return (codigo, mensaje);
    }

    // ── SP_INSERTAR_CUOTA ─────────────────────────────────────────────────────
    public Task<(int CodigoResultado, string MensajeResultado)>
        InsertarCuotaAsync(long documentoId, CuotaPago cuota)
        => OracleRetry.EjecutarAsync(() => InsertarCuotaAsyncCore(documentoId, cuota), _logger, nameof(InsertarCuotaAsync));

    private async Task<(int CodigoResultado, string MensajeResultado)>
        InsertarCuotaAsyncCore(long documentoId, CuotaPago cuota)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = "PKG_LC_LOGISTICA.SP_INSERTAR_CUOTA";
        cmd.CommandType    = System.Data.CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = 60;

        AddParam(cmd, "P_DOCUMENTO_ID",    OracleDbType.Decimal,  documentoId);
        AddParam(cmd, "P_NUMERO_CUOTA",    OracleDbType.Varchar2, cuota.NumeroCuota);
        AddParam(cmd, "P_FECHA_VENCIMIENTO", OracleDbType.Date,  cuota.FechaVencimiento as object ?? DBNull.Value);
        AddParam(cmd, "P_MONTO",           OracleDbType.Decimal,  cuota.Monto);
        AddParam(cmd, "P_MONEDA",          OracleDbType.Varchar2, cuota.Moneda);

        var pCod = new OracleParameter("P_CODIGO_RESULTADO",  OracleDbType.Int32)
                   { Direction = System.Data.ParameterDirection.Output };
        var pMsg = new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 4000)
                   { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCod);
        cmd.Parameters.Add(pMsg);

        await cmd.ExecuteNonQueryAsync();

        int    codigo  = pCod.Value is OracleDecimal oc ? (int)oc.Value   : -1;
        string mensaje = pMsg.Value?.ToString() ?? string.Empty;
        return (codigo, mensaje);
    }

    // ── SP_REGISTRAR_ERROR ────────────────────────────────────────────────────
    public async Task<(int CodigoResultado, string MensajeResultado)>
        RegistrarErrorAsync(string nombreArchivo, string extension, string cuentaCorreo,
                            string asunto, string remitente, string tipoError,
                            string mensajeError, string stackTrace, string contenidoAdjunto)
    {
        try
        {
            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText    = "PKG_LC_LOGISTICA.SP_REGISTRAR_ERROR";
            cmd.CommandType    = System.Data.CommandType.StoredProcedure;
            cmd.BindByName     = true;
            cmd.CommandTimeout = 60;

            AddParam(cmd, "P_NOMBRE_ARCHIVO",    OracleDbType.Varchar2, nombreArchivo);
            AddParam(cmd, "P_EXTENSION_ARCHIVO", OracleDbType.Varchar2, extension);
            AddParam(cmd, "P_CUENTA_CORREO",     OracleDbType.Varchar2, cuentaCorreo);
            AddParam(cmd, "P_ASUNTO_CORREO",     OracleDbType.Varchar2, asunto);
            AddParam(cmd, "P_REMITENTE_CORREO",  OracleDbType.Varchar2, remitente);
            AddParam(cmd, "P_TIPO_ERROR",      OracleDbType.Varchar2, tipoError);
            AddParam(cmd, "P_MENSAJE_ERROR",   OracleDbType.Varchar2, mensajeError);

            cmd.Parameters.Add(new OracleParameter("P_STACK_TRACE", OracleDbType.Clob)
            {
                Direction = System.Data.ParameterDirection.Input,
                Value     = string.IsNullOrEmpty(stackTrace) ? DBNull.Value : (object)stackTrace,
            });
            cmd.Parameters.Add(new OracleParameter("P_CONTENIDO_ADJUNTO", OracleDbType.Clob)
            {
                Direction = System.Data.ParameterDirection.Input,
                Value     = string.IsNullOrEmpty(contenidoAdjunto) ? DBNull.Value : (object)contenidoAdjunto,
            });

            var pCod = new OracleParameter("P_CODIGO_RESULTADO",  OracleDbType.Int32)
                       { Direction = System.Data.ParameterDirection.Output };
            var pMsg = new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 4000)
                       { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(pCod);
            cmd.Parameters.Add(pMsg);

            await cmd.ExecuteNonQueryAsync();

            int    codigo  = pCod.Value is OracleDecimal oc ? (int)oc.Value   : -1;
            string mensaje = pMsg.Value?.ToString() ?? string.Empty;
            return (codigo, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar error en Oracle para el archivo '{Archivo}'.", nombreArchivo);
            return (-1, ex.Message);
        }
    }

    // ── SP_GUARDAR_PDF_ADJUNTO ────────────────────────────────────────────────
    public async Task<(long IdGenerado, int CodigoResultado, string MensajeResultado)>
        GuardarAdjuntoPdfAsync(AdjuntoPdf pdf)
    {
        try
        {
            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText    = "PKG_LC_LOGISTICA.SP_GUARDAR_PDF_ADJUNTO";
            cmd.CommandType    = System.Data.CommandType.StoredProcedure;
            cmd.BindByName     = true;
            cmd.CommandTimeout = 60;

            AddParam(cmd, "P_NOMBRE_ARCHIVO",    OracleDbType.Varchar2, pdf.NombreArchivo);
            AddParam(cmd, "P_CUENTA_CORREO",     OracleDbType.Varchar2, pdf.CuentaCorreo);
            AddParam(cmd, "P_ASUNTO_CORREO",     OracleDbType.Varchar2, pdf.AsuntoCorreo);
            AddParam(cmd, "P_REMITENTE_CORREO",  OracleDbType.Varchar2, pdf.RemitenteCorreo);
            AddParam(cmd, "P_FECHA_CORREO",      OracleDbType.Date,     pdf.FechaCorreo as object ?? DBNull.Value);

            cmd.Parameters.Add(new OracleParameter("P_CONTENIDO_PDF", OracleDbType.Blob)
            {
                Direction = System.Data.ParameterDirection.Input,
                Value     = pdf.Contenido.Length == 0 ? DBNull.Value : (object)pdf.Contenido,
            });

            var pId  = new OracleParameter("P_ID_GENERADO",      OracleDbType.Decimal)
                       { Direction = System.Data.ParameterDirection.Output };
            var pCod = new OracleParameter("P_CODIGO_RESULTADO",  OracleDbType.Int32)
                       { Direction = System.Data.ParameterDirection.Output };
            var pMsg = new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 4000)
                       { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(pId);
            cmd.Parameters.Add(pCod);
            cmd.Parameters.Add(pMsg);

            await cmd.ExecuteNonQueryAsync();

            long   idGen   = pId.Value  is OracleDecimal od ? (long)od.Value  : 0;
            int    codigo  = pCod.Value is OracleDecimal oc ? (int)oc.Value   : -1;
            string mensaje = pMsg.Value?.ToString() ?? string.Empty;
            return (idGen, codigo, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar PDF adjunto '{Archivo}' en Oracle.", pdf.NombreArchivo);
            return (0, -1, ex.Message);
        }
    }

    // ── SP_REGISTRAR_ARCHIVO ──────────────────────────────────────────────────
    public async Task RegistrarArchivoAsync(
        long? documentoId, string tipoArchivo, string nombreOriginal, string nombreGuardado, string rutaArchivo)
    {
        // Sin relación con FH_LC_DOCUMENTO no se inserta en FH_LECTCORREOS_ARCHIVOS.
        if (documentoId is null)
        {
            _logger.LogDebug(
                "RegistrarArchivo omitido para '{Nombre}' ({Tipo}): sin DOCUMENTO_ID asociado.",
                nombreOriginal, tipoArchivo);
            return;
        }

        try
        {
            using var conn = new OracleConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText    = "PKG_LC_LOGISTICA.SP_REGISTRAR_ARCHIVO";
            cmd.CommandType    = System.Data.CommandType.StoredProcedure;
            cmd.BindByName     = true;
            cmd.CommandTimeout = 60;

            AddParam(cmd, "P_DOCUMENTO_ID",   OracleDbType.Decimal,  documentoId.Value);
            AddParam(cmd, "P_TIPO_ARCHIVO",   OracleDbType.Varchar2, tipoArchivo);
            AddParam(cmd, "P_NOMBRE_ORIGINAL",OracleDbType.Varchar2, nombreOriginal);
            AddParam(cmd, "P_NOMBRE_GUARDADO",OracleDbType.Varchar2, nombreGuardado);
            AddParam(cmd, "P_RUTA_ARCHIVO",   OracleDbType.Varchar2, rutaArchivo);

            var pCod = new OracleParameter("P_CODIGO_RESULTADO",  OracleDbType.Int32)
                       { Direction = System.Data.ParameterDirection.Output };
            var pMsg = new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 4000)
                       { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(pCod);
            cmd.Parameters.Add(pMsg);

            await cmd.ExecuteNonQueryAsync();

            int    codigo  = pCod.Value is OracleDecimal oc ? (int)oc.Value : -1;
            string mensaje = pMsg.Value?.ToString() ?? string.Empty;
            if (codigo != 0)
                _logger.LogWarning(
                    "RegistrarArchivo tipo {Tipo} '{Nombre}': código {Cod} - {Msg}",
                    tipoArchivo, nombreOriginal, codigo, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar archivo '{Nombre}' en BD.", nombreOriginal);
        }
    }

    // ── SP_MARCAR_ERROR_REVISADO ──────────────────────────────────────────────
    public async Task<(int CodigoResultado, string MensajeResultado)>
        MarcarErrorRevisadoAsync(long id, string observaciones)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_MARCAR_ERROR_REVISADO";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_ID",            OracleDbType.Decimal,  id);
        AddParam(cmd, "P_OBSERVACIONES", OracleDbType.Varchar2, observaciones);

        var pCod = new OracleParameter("P_CODIGO_RESULTADO",  OracleDbType.Int32)
                   { Direction = System.Data.ParameterDirection.Output };
        var pMsg = new OracleParameter("P_MENSAJE_RESULTADO", OracleDbType.Varchar2, 4000)
                   { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCod);
        cmd.Parameters.Add(pMsg);

        await cmd.ExecuteNonQueryAsync();

        int    codigo  = pCod.Value is OracleDecimal oc ? (int)oc.Value : -1;
        string mensaje = pMsg.Value?.ToString() ?? string.Empty;
        return (codigo, mensaje);
    }

    // ── SP_LISTAR_DOCUMENTOS ──────────────────────────────────────────────────
    public async Task<IReadOnlyList<DocumentoResumen>>
        ListarDocumentosAsync(FiltroDocumentos filtro)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_LISTAR_DOCUMENTOS";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_RUC_EMISOR",     OracleDbType.Varchar2, filtro.RucEmisor);
        AddParam(cmd, "P_RUC_RECEPTOR",   OracleDbType.Varchar2, filtro.RucReceptor);
        AddParam(cmd, "P_TIPO_XML",        OracleDbType.Varchar2, filtro.TipoXml);
        AddParam(cmd, "P_TIPO_DOCUMENTO",  OracleDbType.Varchar2, filtro.TipoDocumento);
        AddParam(cmd, "P_ESTADO",          OracleDbType.Varchar2, filtro.Estado);
        AddParam(cmd, "P_FECHA_DESDE",     OracleDbType.Date,     filtro.FechaDesde as object ?? DBNull.Value);
        AddParam(cmd, "P_FECHA_HASTA",     OracleDbType.Date,     filtro.FechaHasta as object ?? DBNull.Value);
        AddParam(cmd, "P_NUMERO_PEDIDO",   OracleDbType.Varchar2, filtro.NumeroPedido);
        AddParam(cmd, "P_NUMERO_GUIA",     OracleDbType.Varchar2, filtro.NumeroGuia);
        AddParam(cmd, "P_CUENTA_CORREO",   OracleDbType.Varchar2, filtro.CuentaCorreo);

        var pCursor = new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                      { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCursor);

        await cmd.ExecuteNonQueryAsync();

        var lista = new List<DocumentoResumen>();
        using var rc     = (OracleRefCursor)pCursor.Value;
        using var reader = rc.GetDataReader();
        while (reader.Read())
            lista.Add(MapDocumentoResumen(reader));

        return lista;
    }

    // ── SP_OBTENER_DOCUMENTO ──────────────────────────────────────────────────
    public async Task<(DocumentoResumen? Cabecera, IReadOnlyList<LineaDocumento> Lineas, IReadOnlyList<CuotaPago> Cuotas)>
        ObtenerDocumentoAsync(long id)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_OBTENER_DOCUMENTO";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_ID", OracleDbType.Decimal, id);

        var pCab = new OracleParameter("P_CURSOR_CABECERA", OracleDbType.RefCursor)
                   { Direction = System.Data.ParameterDirection.Output };
        var pLin = new OracleParameter("P_CURSOR_LINEAS",   OracleDbType.RefCursor)
                   { Direction = System.Data.ParameterDirection.Output };
        var pCuo = new OracleParameter("P_CURSOR_CUOTAS",   OracleDbType.RefCursor)
                   { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCab);
        cmd.Parameters.Add(pLin);
        cmd.Parameters.Add(pCuo);

        await cmd.ExecuteNonQueryAsync();

        DocumentoResumen? cabecera;
        using (var rc = (OracleRefCursor)pCab.Value)
        using (var reader = rc.GetDataReader())
            cabecera = reader.Read() ? MapDocumentoResumen(reader) : null;

        var lineas = new List<LineaDocumento>();
        using (var rc = (OracleRefCursor)pLin.Value)
        using (var reader = rc.GetDataReader())
            while (reader.Read()) lineas.Add(MapLineaDocumento(reader));

        var cuotas = new List<CuotaPago>();
        using (var rc = (OracleRefCursor)pCuo.Value)
        using (var reader = rc.GetDataReader())
            while (reader.Read()) cuotas.Add(MapCuotaPago(reader));

        return (cabecera, lineas, cuotas);
    }

    // ── SELECT ID por NUMERO_DOCUMENTO ───────────────────────────────────────
    public async Task<long?> ObtenerDocumentoIdAsync(string numeroDocumento)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ID FROM FH_LC_DOCUMENTO WHERE NUMERO_DOCUMENTO = :P_NUM AND ROWNUM = 1";
        cmd.CommandType = System.Data.CommandType.Text;
        cmd.BindByName  = true;

        AddParam(cmd, "P_NUM", OracleDbType.Varchar2, numeroDocumento);

        var val = await cmd.ExecuteScalarAsync();
        if (val is null || val == DBNull.Value) return null;
        if (val is OracleDecimal od) return (long)od.Value;
        return Convert.ToInt64(val);
    }

    // ── SELECT ID por RUC_EMISOR + SERIE + CORRELATIVO ───────────────────────
    public async Task<long?> ObtenerDocumentoIdPorRucYSerieAsync(
        string rucEmisor, string serie, long correlativo)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT ID FROM FH_LC_DOCUMENTO
                             WHERE RUC_EMISOR = :P_RUC
                               AND SERIE      = :P_SERIE
                               AND TO_NUMBER(CORRELATIVO) = :P_CORR
                               AND ROWNUM = 1";
        cmd.CommandType = System.Data.CommandType.Text;
        cmd.BindByName  = true;

        AddParam(cmd, "P_RUC",  OracleDbType.Varchar2, rucEmisor);
        AddParam(cmd, "P_SERIE", OracleDbType.Varchar2, serie);
        AddParam(cmd, "P_CORR", OracleDbType.Decimal,  correlativo);

        var val = await cmd.ExecuteScalarAsync();
        if (val is null || val == DBNull.Value) return null;
        if (val is OracleDecimal od) return (long)od.Value;
        return Convert.ToInt64(val);
    }

    // ── SP_DOC_POR_VENCER ─────────────────────────────────────────────────────
    public async Task<IReadOnlyList<DocumentoPorVencer>>
        ObtenerDocumentosPorVencerAsync(int diasAdelante = 30)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_DOC_POR_VENCER";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_DIAS_ADELANTE", OracleDbType.Int32, diasAdelante);

        var pCursor = new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                      { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCursor);

        await cmd.ExecuteNonQueryAsync();

        var lista = new List<DocumentoPorVencer>();
        using var rc     = (OracleRefCursor)pCursor.Value;
        using var reader = rc.GetDataReader();
        while (reader.Read())
            lista.Add(MapDocumentoPorVencer(reader));

        return lista;
    }

    // ── SP_LISTAR_ERRORES ─────────────────────────────────────────────────────
    public async Task<IReadOnlyList<ErrorProcesamiento>>
        ListarErroresAsync(char procesado = 'N', DateTime? fechaDesde = null)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_LISTAR_ERRORES";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_PROCESADO",   OracleDbType.Char, procesado.ToString());
        AddParam(cmd, "P_FECHA_DESDE", OracleDbType.Date, fechaDesde as object ?? DBNull.Value);

        var pCursor = new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                      { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCursor);

        await cmd.ExecuteNonQueryAsync();

        var lista = new List<ErrorProcesamiento>();
        using var rc     = (OracleRefCursor)pCursor.Value;
        using var reader = rc.GetDataReader();
        while (reader.Read())
            lista.Add(MapErrorProcesamiento(reader));

        return lista;
    }

    // ── SP_RESUMEN_POR_CUENTA ─────────────────────────────────────────────────
    public async Task<IReadOnlyList<ResumenPorCuenta>>
        ObtenerResumenPorCuentaAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_RESUMEN_POR_CUENTA";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_FECHA_DESDE", OracleDbType.Date, fechaDesde);
        AddParam(cmd, "P_FECHA_HASTA", OracleDbType.Date, fechaHasta);

        var pCursor = new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                      { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCursor);

        await cmd.ExecuteNonQueryAsync();

        var lista = new List<ResumenPorCuenta>();
        using var rc     = (OracleRefCursor)pCursor.Value;
        using var reader = rc.GetDataReader();
        while (reader.Read())
            lista.Add(MapResumenPorCuenta(reader));

        return lista;
    }

    // ── SP_RESUMEN_POR_PROVEEDOR ──────────────────────────────────────────────
    public async Task<IReadOnlyList<ResumenPorProveedor>>
        ObtenerResumenPorProveedorAsync(DateTime fechaDesde, DateTime fechaHasta)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_RESUMEN_POR_PROVEEDOR";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_FECHA_DESDE", OracleDbType.Date, fechaDesde);
        AddParam(cmd, "P_FECHA_HASTA", OracleDbType.Date, fechaHasta);

        var pCursor = new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                      { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCursor);

        await cmd.ExecuteNonQueryAsync();

        var lista = new List<ResumenPorProveedor>();
        using var rc     = (OracleRefCursor)pCursor.Value;
        using var reader = rc.GetDataReader();
        while (reader.Read())
            lista.Add(MapResumenPorProveedor(reader));

        return lista;
    }

    // ── SP_GUIAS_POR_TRANSPORTISTA ────────────────────────────────────────────
    public async Task<IReadOnlyList<GuiaPorTransportista>>
        ObtenerGuiasPorTransportistaAsync(string rucTransportista, DateTime? fechaDesde = null)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_GUIAS_POR_TRANSPORTISTA";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_RUC_TRANSPORTISTA", OracleDbType.Varchar2, rucTransportista);
        AddParam(cmd, "P_FECHA_DESDE",       OracleDbType.Date,     fechaDesde as object ?? DBNull.Value);

        var pCursor = new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                      { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCursor);

        await cmd.ExecuteNonQueryAsync();

        var lista = new List<GuiaPorTransportista>();
        using var rc     = (OracleRefCursor)pCursor.Value;
        using var reader = rc.GetDataReader();
        while (reader.Read())
            lista.Add(MapGuiaPorTransportista(reader));

        return lista;
    }

    // ── SP_BUSCAR_POR_PEDIDO ──────────────────────────────────────────────────
    public async Task<IReadOnlyList<DocumentoPorPedido>>
        BuscarPorPedidoAsync(string numeroPedido)
    {
        using var conn = new OracleConnection(_connStr);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PKG_LC_LOGISTICA.SP_BUSCAR_POR_PEDIDO";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.BindByName  = true;

        AddParam(cmd, "P_NUMERO_PEDIDO", OracleDbType.Varchar2, numeroPedido);

        var pCursor = new OracleParameter("P_CURSOR", OracleDbType.RefCursor)
                      { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCursor);

        await cmd.ExecuteNonQueryAsync();

        var lista = new List<DocumentoPorPedido>();
        using var rc     = (OracleRefCursor)pCursor.Value;
        using var reader = rc.GetDataReader();
        while (reader.Read())
            lista.Add(MapDocumentoPorPedido(reader));

        return lista;
    }

    // ── Helpers de mapeo ──────────────────────────────────────────────────────

    private static DocumentoResumen MapDocumentoResumen(OracleDataReader r) => new()
    {
        Id                    = RLong(r, "ID"),
        NombreArchivo         = RStr(r,  "NOMBRE_ARCHIVO"),
        CuentaCorreo          = RStr(r,  "CUENTA_CORREO"),
        AsuntoCorreo          = RStr(r,  "ASUNTO_CORREO"),
        RemitenteCorreo       = RStr(r,  "REMITENTE_CORREO"),
        FechaCorreo           = RDateN(r,"FECHA_CORREO"),
        TipoXml               = RStr(r,  "TIPO_XML"),
        TipoDocumento         = RStr(r,  "TIPO_DOCUMENTO"),
        DescTipoDocumento     = RStr(r,  "DESC_TIPO_DOCUMENTO"),
        Serie                 = RStr(r,  "SERIE"),
        Correlativo           = RStr(r,  "CORRELATIVO"),
        NumeroDocumento       = RStr(r,  "NUMERO_DOCUMENTO"),
        FechaEmision          = RDateN(r,"FECHA_EMISION"),
        HoraEmision           = RStr(r,  "HORA_EMISION"),
        RucEmisor             = RStr(r,  "RUC_EMISOR"),
        RazonSocialEmisor     = RStr(r,  "RAZON_SOCIAL_EMISOR"),
        NombreComercialEmisor = RStr(r,  "NOMBRE_COMERCIAL_EMISOR"),
        DireccionEmisor       = RStr(r,  "DIRECCION_EMISOR"),
        RucReceptor           = RStr(r,  "RUC_RECEPTOR"),
        RazonSocialReceptor   = RStr(r,  "RAZON_SOCIAL_RECEPTOR"),
        DireccionReceptor     = RStr(r,  "DIRECCION_RECEPTOR"),
        Moneda                = RStr(r,  "MONEDA"),
        BaseImponible         = RDec(r,  "BASE_IMPONIBLE"),
        TotalIgv              = RDec(r,  "TOTAL_IGV"),
        TotalExonerado        = RDec(r,  "TOTAL_EXONERADO"),
        TotalInafecto         = RDec(r,  "TOTAL_INAFECTO"),
        TotalGratuito         = RDec(r,  "TOTAL_GRATUITO"),
        TotalPagar            = RDec(r,  "TOTAL_PAGAR"),
        FormaPago             = RStr(r,  "FORMA_PAGO"),
        FechaVencimiento      = RDateN(r,"FECHA_VENCIMIENTO"),
        MontoNetoPendiente    = RDec(r,  "MONTO_NETO_PENDIENTE"),
        TieneDetraccion       = RStr(r,  "TIENE_DETRACCION"),
        PctDetraccion         = RDec(r,  "PCT_DETRACCION"),
        MontoDetraccion       = RDec(r,  "MONTO_DETRACCION"),
        NumeroPedido          = RStr(r,  "NUMERO_PEDIDO"),
        NumeroGuia            = RStr(r,  "NUMERO_GUIA"),
        NumeroDocRef          = RStr(r,  "NUMERO_DOC_REF"),
        ModalidadTraslado     = RStr(r,  "MODALIDAD_TRASLADO"),
        MotivoTraslado        = RStr(r,  "MOTIVO_TRASLADO"),
        PesoBruto             = RDec(r,  "PESO_BRUTO"),
        UnidadPeso            = RStr(r,  "UNIDAD_PESO"),
        FechaInicioTraslado   = RDateN(r,"FECHA_INICIO_TRASLADO"),
        RucTransportista      = RStr(r,  "RUC_TRANSPORTISTA"),
        RazonSocTransportista = RStr(r,  "RAZON_SOC_TRANSPORTISTA"),
        PlacaVehiculo         = RStr(r,  "PLACA_VEHICULO"),
        NombreConductor       = RStr(r,  "NOMBRE_CONDUCTOR"),
        Estado                = RStr(r,  "ESTADO"),
        FechaProcesamiento    = RDate(r, "FECHA_PROCESAMIENTO"),
        Observaciones         = RStr(r,  "OBSERVACIONES"),
        CantLineas            = TryRInt(r,"CANT_LINEAS"),
        CantCuotas            = TryRInt(r,"CANT_CUOTAS"),
    };

    private static LineaDocumento MapLineaDocumento(OracleDataReader r) => new()
    {
        NumeroLinea    = RInt(r, "NUMERO_LINEA"),
        Descripcion    = RStr(r, "DESCRIPCION"),
        NombreItem     = RStr(r, "NOMBRE_ITEM"),
        CodigoProducto = RStr(r, "CODIGO_PRODUCTO"),
        CodigoUNSPSC   = RStr(r, "CODIGO_UNSPSC"),
        Cantidad       = RDec(r, "CANTIDAD"),
        UnidadMedida   = RStr(r, "UNIDAD_MEDIDA"),
        PrecioUnitario = RDec(r, "PRECIO_UNITARIO"),
        PrecioConIgv   = RDec(r, "PRECIO_CON_IGV"),
        EsGratuito     = RStr(r, "ES_GRATUITO") == "S",
        SubTotal       = RDec(r, "SUB_TOTAL"),
        Igv            = RDec(r, "IGV"),
        TotalLinea     = RDec(r, "TOTAL_LINEA"),
        AfectacionIgv  = RStr(r, "AFECTACION_IGV"),
        PorcentajeIgv  = RDec(r, "PORCENTAJE_IGV"),
        Lote           = RStr(r, "LOTE"),
        FechaVencLote  = RDateN(r,"FECHA_VENC_LOTE"),
    };

    private static CuotaPago MapCuotaPago(OracleDataReader r) => new()
    {
        NumeroCuota      = RStr(r,  "NUMERO_CUOTA"),
        FechaVencimiento = RDateN(r,"FECHA_VENCIMIENTO"),
        Monto            = RDec(r,  "MONTO"),
        Moneda           = RStr(r,  "MONEDA"),
    };

    private static DocumentoPorVencer MapDocumentoPorVencer(OracleDataReader r) => new()
    {
        Id                 = RLong(r, "ID"),
        NumeroDocumento    = RStr(r,  "NUMERO_DOCUMENTO"),
        DescTipoDocumento  = RStr(r,  "DESC_TIPO_DOCUMENTO"),
        RucEmisor          = RStr(r,  "RUC_EMISOR"),
        RazonSocialEmisor  = RStr(r,  "RAZON_SOCIAL_EMISOR"),
        CuentaCorreo       = RStr(r,  "CUENTA_CORREO"),
        FechaEmision       = RDateN(r,"FECHA_EMISION"),
        FechaVencimiento   = RDateN(r,"FECHA_VENCIMIENTO"),
        DiasParaVencer     = RInt(r,  "DIAS_PARA_VENCER"),
        Moneda             = RStr(r,  "MONEDA"),
        TotalPagar         = RDec(r,  "TOTAL_PAGAR"),
        MontoNetoPendiente = RDec(r,  "MONTO_NETO_PENDIENTE"),
        TieneDetraccion    = RStr(r,  "TIENE_DETRACCION"),
        MontoDetraccion    = RDec(r,  "MONTO_DETRACCION"),
        NumeroPedido       = RStr(r,  "NUMERO_PEDIDO"),
        NumeroGuia         = RStr(r,  "NUMERO_GUIA"),
        FormaPago          = RStr(r,  "FORMA_PAGO"),
        Prioridad          = RStr(r,  "PRIORIDAD"),
    };

    private static ErrorProcesamiento MapErrorProcesamiento(OracleDataReader r) => new()
    {
        Id               = RLong(r, "ID"),
        NombreArchivo    = RStr(r,  "NOMBRE_ARCHIVO"),
        ExtensionArchivo = RStr(r,  "EXTENSION_ARCHIVO"),
        CuentaCorreo     = RStr(r,  "CUENTA_CORREO"),
        AsuntoCorreo     = RStr(r,  "ASUNTO_CORREO"),
        RemitenteCorreo  = RStr(r,  "REMITENTE_CORREO"),
        TipoError        = RStr(r,  "TIPO_ERROR"),
        MensajeError     = RStr(r,  "MENSAJE_ERROR"),
        FechaError       = RDate(r, "FECHA_ERROR"),
        Procesado        = RStr(r,  "PROCESADO"),
        FechaRevision    = RDateN(r,"FECHA_REVISION"),
        Observaciones    = RStr(r,  "OBSERVACIONES"),
    };

    private static ResumenPorCuenta MapResumenPorCuenta(OracleDataReader r) => new()
    {
        CuentaCorreo      = RStr(r, "CUENTA_CORREO"),
        TotalDocumentos   = RInt(r, "TOTAL_DOCUMENTOS"),
        FacturasBoletas   = RInt(r, "FACTURAS_BOLETAS"),
        GuiasRemision     = RInt(r, "GUIAS_REMISION"),
        NotasCredito      = RInt(r, "NOTAS_CREDITO"),
        NotasDebito       = RInt(r, "NOTAS_DEBITO"),
        Desconocidos      = RInt(r, "DESCONOCIDOS"),
        Procesados        = RInt(r, "PROCESADOS"),
        ConError          = RInt(r, "CON_ERROR"),
        Duplicados        = RInt(r, "DUPLICADOS"),
        Ignorados         = RInt(r, "IGNORADOS"),
        TotalSoles        = RDec(r, "TOTAL_SOLES"),
        TotalDolares      = RDec(r, "TOTAL_DOLARES"),
        TotalDetracciones = RDec(r, "TOTAL_DETRACCIONES"),
    };

    private static ResumenPorProveedor MapResumenPorProveedor(OracleDataReader r) => new()
    {
        RucEmisor          = RStr(r,  "RUC_EMISOR"),
        RazonSocialEmisor  = RStr(r,  "RAZON_SOCIAL_EMISOR"),
        TotalDocumentos    = RInt(r,  "TOTAL_DOCUMENTOS"),
        FacturasBoletas    = RInt(r,  "FACTURAS_BOLETAS"),
        GuiasRemision      = RInt(r,  "GUIAS_REMISION"),
        NotasCredito       = RInt(r,  "NOTAS_CREDITO"),
        NotasDebito        = RInt(r,  "NOTAS_DEBITO"),
        TotalSoles         = RDec(r,  "TOTAL_SOLES"),
        TotalDolares       = RDec(r,  "TOTAL_DOLARES"),
        TotalDetracciones  = RDec(r,  "TOTAL_DETRACCIONES"),
        PrimeraFactura     = RDateN(r,"PRIMERA_FACTURA"),
        UltimaFactura      = RDateN(r,"ULTIMA_FACTURA"),
        PromedioImporte    = RDec(r,  "PROMEDIO_IMPORTE"),
    };

    private static GuiaPorTransportista MapGuiaPorTransportista(OracleDataReader r) => new()
    {
        Id                   = RLong(r, "ID"),
        NumeroDocumento       = RStr(r,  "NUMERO_DOCUMENTO"),
        FechaEmision          = RDateN(r,"FECHA_EMISION"),
        RucEmisor             = RStr(r,  "RUC_EMISOR"),
        RazonSocialEmisor     = RStr(r,  "RAZON_SOCIAL_EMISOR"),
        RucReceptor           = RStr(r,  "RUC_RECEPTOR"),
        RazonSocialReceptor   = RStr(r,  "RAZON_SOCIAL_RECEPTOR"),
        RucTransportista      = RStr(r,  "RUC_TRANSPORTISTA"),
        RazonSocTransportista = RStr(r,  "RAZON_SOC_TRANSPORTISTA"),
        NombreConductor       = RStr(r,  "NOMBRE_CONDUCTOR"),
        LicenciaConductor     = RStr(r,  "LICENCIA_CONDUCTOR"),
        NroDocConductor       = RStr(r,  "NRO_DOC_CONDUCTOR"),
        PlacaVehiculo         = RStr(r,  "PLACA_VEHICULO"),
        MarcaVehiculo         = RStr(r,  "MARCA_VEHICULO"),
        ModalidadTraslado     = RStr(r,  "MODALIDAD_TRASLADO"),
        MotivoTraslado        = RStr(r,  "MOTIVO_TRASLADO"),
        ModoTransporte        = RStr(r,  "MODO_TRANSPORTE"),
        PesoBruto             = RDec(r,  "PESO_BRUTO"),
        UnidadPeso            = RStr(r,  "UNIDAD_PESO"),
        FechaInicioTraslado   = RDateN(r,"FECHA_INICIO_TRASLADO"),
        FechaFinTraslado      = RDateN(r,"FECHA_FIN_TRASLADO"),
        UbigeoOrigen          = RStr(r,  "UBIGEO_ORIGEN"),
        DirOrigen             = RStr(r,  "DIR_ORIGEN"),
        UbigeoDestino         = RStr(r,  "UBIGEO_DESTINO"),
        DirDestino            = RStr(r,  "DIR_DESTINO"),
        NumeroPedido          = RStr(r,  "NUMERO_PEDIDO"),
        Estado                = RStr(r,  "ESTADO"),
        CuentaCorreo          = RStr(r,  "CUENTA_CORREO"),
        CantItems             = RInt(r,  "CANT_ITEMS"),
    };

    private static DocumentoPorPedido MapDocumentoPorPedido(OracleDataReader r) => new()
    {
        Id                  = RLong(r, "ID"),
        TipoXml             = RStr(r,  "TIPO_XML"),
        DescTipoDocumento   = RStr(r,  "DESC_TIPO_DOCUMENTO"),
        NumeroDocumento     = RStr(r,  "NUMERO_DOCUMENTO"),
        FechaEmision        = RDateN(r,"FECHA_EMISION"),
        RucEmisor           = RStr(r,  "RUC_EMISOR"),
        RazonSocialEmisor   = RStr(r,  "RAZON_SOCIAL_EMISOR"),
        Moneda              = RStr(r,  "MONEDA"),
        TotalPagar          = RDec(r,  "TOTAL_PAGAR"),
        FormaPago           = RStr(r,  "FORMA_PAGO"),
        FechaVencimiento    = RDateN(r,"FECHA_VENCIMIENTO"),
        TieneDetraccion     = RStr(r,  "TIENE_DETRACCION"),
        MontoDetraccion     = RDec(r,  "MONTO_DETRACCION"),
        NumeroGuia          = RStr(r,  "NUMERO_GUIA"),
        PlacaVehiculo       = RStr(r,  "PLACA_VEHICULO"),
        NombreConductor     = RStr(r,  "NOMBRE_CONDUCTOR"),
        FechaInicioTraslado = RDateN(r,"FECHA_INICIO_TRASLADO"),
        DirOrigen           = RStr(r,  "DIR_ORIGEN"),
        DirDestino          = RStr(r,  "DIR_DESTINO"),
        Estado              = RStr(r,  "ESTADO"),
        CuentaCorreo        = RStr(r,  "CUENTA_CORREO"),
        NombreArchivo       = RStr(r,  "NOMBRE_ARCHIVO"),
        CantLineas          = RInt(r,  "CANT_LINEAS"),
    };

    // ── Lectores seguros de OracleDataReader ──────────────────────────────────

    private static string RStr(OracleDataReader r, string col)
    {
        var v = r[col];
        return v == DBNull.Value ? string.Empty : v.ToString()!.Trim();
    }

    private static long RLong(OracleDataReader r, string col)
    {
        var v = r[col];
        return v == DBNull.Value ? 0 : Convert.ToInt64(v);
    }

    private static int RInt(OracleDataReader r, string col)
    {
        var v = r[col];
        return v == DBNull.Value ? 0 : Convert.ToInt32(v);
    }

    private static decimal RDec(OracleDataReader r, string col)
    {
        var v = r[col];
        return v == DBNull.Value ? 0m : Convert.ToDecimal(v);
    }

    private static DateTime RDate(OracleDataReader r, string col)
    {
        var v = r[col];
        return v == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(v);
    }

    private static DateTime? RDateN(OracleDataReader r, string col)
    {
        var v = r[col];
        return v == DBNull.Value ? null : Convert.ToDateTime(v);
    }

    /// <summary>Lee un int de una columna que puede no existir en el cursor (p.ej. CANT_LINEAS).</summary>
    private static int TryRInt(OracleDataReader r, string col)
    {
        try   { return RInt(r, col); }
        catch (IndexOutOfRangeException) { return 0; }
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private static void AddParam(OracleCommand cmd, string name, OracleDbType type, object? value)
    {
        cmd.Parameters.Add(new OracleParameter(name, type)
        {
            Direction = System.Data.ParameterDirection.Input,
            Value     = value ?? DBNull.Value,
        });
    }
}
