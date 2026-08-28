namespace FabricaHilos.Models.Sgc
{
    /// <summary>
    /// DTO para la tabla SIG.REQ_CERT (Requerimiento de Certificado)
    /// </summary>
    public class ReqCertDto
    {
        public int NumReq { get; set; }
        public string? Tipo { get; set; }
        public DateTime? Fecha { get; set; }
        public string? NumCer { get; set; }
        public string? CodCliente { get; set; }
        public string? CodArt { get; set; }
        public string? CodVende { get; set; }
        public string? TipoDoc { get; set; }
        public string? Serie { get; set; }
        public string? Numero { get; set; }
        public int? Estado { get; set; }
        public string? AAduser { get; set; }
        public DateTime? AAdfecha { get; set; }
        public string? AMduser { get; set; }
        public DateTime? AMdfecha { get; set; }

        // Campos adicionales (joins)
        public string? RazonSocial { get; set; }
        public string? Ruc { get; set; }

        // Usado solo por CargaTcFibra (TIPO='C'): Nº de REQUISICION asociada (informativo, primera encontrada)
        public decimal? NumRequisicion { get; set; }
        public string? Observacion { get; set; }

        // Usado solo por CargaTcFibra (TIPO='C'): todas las Órdenes de Compra distintas
        // asociadas al requerimiento (de todos sus ítems), separadas por coma.
        public string? Ocs { get; set; }
        public List<string> OcsList => string.IsNullOrEmpty(Ocs)
            ? new List<string>()
            : Ocs.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        // true si ya existe una REQUISICION de servicio (CENTRO_COSTO='230') enlazada a este
        // certificado — una vez registrada, el botón "Crear Requerimiento" debe deshabilitarse.
        public bool TieneRequerimientoCertificado { get; set; }
    }

    /// <summary>
    /// DTO para la tabla SIG.REQ_CERT_D (Detalle de Requerimiento de Certificado)
    /// </summary>
    public class ReqCertDDto
    {
        public int NumReq { get; set; }
        public string? TipoDoc { get; set; }
        public string? Serie { get; set; }
        public string? Numero { get; set; }
        public string? AAduser { get; set; }
        public DateTime? AAdfecha { get; set; }
        public string? AMduser { get; set; }
        public DateTime? AMdfecha { get; set; }
    }

    /// <summary>
    /// DTO para actualizar certificado en REQ_CERT
    /// </summary>
    public class ActualizarCertificadoDto
    {
        public int NumReq { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(30,
            ErrorMessage = "El Nº Certificado no puede superar los 30 caracteres.")]
        public string? NumCer { get; set; }

        // Usado solo por CargaTcFibra (TIPO='C'). REQ_CERT.OBSERVACION es VARCHAR2(200).
        [System.ComponentModel.DataAnnotations.MaxLength(200,
            ErrorMessage = "La Observación no puede superar los 200 caracteres.")]
        public string? Observacion { get; set; }
    }

    /// <summary>
    /// DTO para registrar (un-click) un nuevo requerimiento de Certificado Digital desde
    /// CargaTcFibra/Index. Artículo siempre fijo (X02018). El resto de campos de
    /// REQUISICION/ITEMREQ se completan con los valores fijos usados realmente por SGC
    /// (CENTRO_COSTO=230, RESPONSABLE/COD_SOLICITA=034685, PRIORIDAD=02, IND_SERV=S,
    /// DESTINO=00/230, MONEDA=D) — solo Proveedor/Cantidad/Observación varían caso a caso.
    /// </summary>
    public class RegistrarRequerimientoCertDto
    {
        // REQ_CERT.NUM_REQ (TIPO='C') YA EXISTENTE al que se enlazará este nuevo requerimiento
        // como una fila más de su detalle (REQ_CERT_D).
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue,
            ErrorMessage = "Debe indicar el requerimiento de certificado al que se enlazará.")]
        public int NumReq { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El Proveedor es obligatorio.")]
        [System.ComponentModel.DataAnnotations.MaxLength(200,
            ErrorMessage = "El Proveedor no puede superar los 200 caracteres.")]
        public string Proveedor { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Range(1, 999999,
            ErrorMessage = "La Cantidad debe ser un entero mayor o igual a 1.")]
        public int Cantidad { get; set; } = 1;

        // Se usa tanto en REQUISICION.OBSERVACION (250) como en ITEMREQ.OBSERVACIONES (150);
        // se limita al más estricto de los dos.
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "La Observación es obligatoria.")]
        [System.ComponentModel.DataAnnotations.MaxLength(150,
            ErrorMessage = "La Observación no puede superar los 150 caracteres.")]
        public string Observacion { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para la vista de Cliente (para obtener RUC)
    /// </summary>
    public class ClienteDto
    {
        public string? CodCliente { get; set; }
        public string? Ruc { get; set; }
        public string? RazonSocial { get; set; }
    }

    /// <summary>
    /// DTO para partidas asociadas a un requerimiento de certificado
    /// </summary>
    public class ReqCertPartidaDto
    {
        public string? Partida { get; set; }
        public int? Item { get; set; }
        public string PartidaItem => Item.HasValue ? $"{Partida}-{Item}" : Partida ?? string.Empty;
    }

    /// <summary>
    /// DTO para un ítem de Requisición (documento asociado) de un requerimiento de
    /// certificado de fibra (SIG.REQ_CERT/REQ_CERT_D con TIPO='C'). Cada fila representa
    /// un ítem de DESP_ITEMREQ de la requisición referenciada por REQ_CERT_D.
    /// </summary>
    public class ReqCertFibraDocDto
    {
        public int NumReq { get; set; }

        // Clave de la Requisición (REQUISICION.TIPDOC/SERIE/NUMREQ)
        public string? TipoDoc { get; set; }
        public string? Serie { get; set; }
        public decimal? NumRequisicion { get; set; }
        public DateTime? FechaReq { get; set; }
        public string? EstadoReq { get; set; }
        public string? ObservacionReq { get; set; }

        // Ítem de la requisición (DESP_ITEMREQ + ITEMREQ + ARTICUL)
        public int? Orden { get; set; }
        public string? CodArt { get; set; }
        public string? Articulo { get; set; }
        public decimal? Cantidad { get; set; }
        public string? Unidad { get; set; }
        public string? Moneda { get; set; }

        // "$" para Dólares (MONEDA='D'), "S/" para Soles (MONEDA='S'), o el código crudo
        public string MonedaSimbolo => (Moneda ?? "").ToUpper() switch
        {
            "D" => "$",
            "S" => "S/",
            _ => Moneda ?? "-"
        };

        // Orden de Compra referenciada por el ítem
        public decimal? Oc { get; set; }
        public DateTime? FechaOc { get; set; }
        public string? ObservacionOc { get; set; }

        // Proveedor (SIG.PROVEED)
        public string? CodProveed { get; set; }
        public string? Proveedor { get; set; }
        public string? Ruc { get; set; }

        // Ingreso a almacén (KARDEX_G/KARDEX_D, TP_TRANSAC='11') asociado a la OC
        // (o a la requisición directa si aún no hay OC). Informativo, solo lectura.
        public decimal? CantidadIngresada { get; set; }
        public DateTime? FechaIngreso { get; set; }

        public bool TieneOc => Oc.HasValue;
        public bool TieneIngreso => FechaIngreso.HasValue;
    }

    /// <summary>
    /// DTO para órdenes de compra asociadas a un requerimiento de certificado
    /// </summary>
    public class ReqCertOrdenCompraDto
    {
        public string? OrdenCompra { get; set; }
    }
}
