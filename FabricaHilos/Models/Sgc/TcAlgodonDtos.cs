namespace FabricaHilos.Models.Sgc
{
    /// <summary>
    /// DTO para la vista SIG.V_SGC_TC_ALGODON_ORG (trazabilidad de compras de
    /// algodón orgánico certificado, reconstruida a partir de REQUISICION,
    /// ORDEN_DE_COMPRA, KARDEX_G/KARDEX_D, ARTICUL y SGC_CERT_ALGODON_ORGANICO).
    /// </summary>
    public class TcAlgodonDto
    {
        // Clave natural del ingreso en KARDEX_G
        public string? CodAlm { get; set; }
        public string? TpTransac { get; set; }
        public decimal? Serie { get; set; }
        public decimal? Numero { get; set; }

        public string? Algodon { get; set; }
        public int? Req { get; set; }
        public decimal? Oc { get; set; }
        public DateTime? FchReq { get; set; }
        public DateTime? FchOc { get; set; }
        public decimal? CantidadQq { get; set; }
        public decimal? CantidadKgAprox { get; set; }
        public string? Factura { get; set; }
        public string? Guia { get; set; }
        // Lote y Factura del TC: sin fuente Oracle confirmada, ocultos desde el 14/08/2026 (ver PKG_SGC_TC_ALGODON.sql)
        public DateTime? FchAtencion { get; set; }
        public string? Tc { get; set; }
        public string? Tipo { get; set; }
        public string? CodProveed { get; set; }
        public string? Proveedor { get; set; }
        public string? DetalleOc { get; set; }
        public string? PendRegistroTc { get; set; }
        public int? IdCert { get; set; }
        public string? UsuarioResponsable { get; set; }
    }

    /// <summary>
    /// DTO para registrar/actualizar el certificado TC de un ingreso
    /// (P_REGISTRAR_CERTIFICADO).
    /// </summary>
    public class RegistrarCertificadoTcAlgodonDto
    {
        public string CodAlm { get; set; } = string.Empty;
        public string TpTransac { get; set; } = string.Empty;
        public decimal Serie { get; set; }
        public decimal Numero { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El número de TC (Transaction Certificate) es obligatorio.")]
        [System.ComponentModel.DataAnnotations.MaxLength(40,
            ErrorMessage = "El N° TC no puede superar los 40 caracteres.")]
        public string? NumTc { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(30,
            ErrorMessage = "El Tipo de certificación no puede superar los 30 caracteres.")]
        public string? TipoCert { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(500,
            ErrorMessage = "La Observación no puede superar los 500 caracteres.")]
        public string? Observacion { get; set; }
    }

    /// <summary>
    /// Resultado de operaciones de mantenimiento del paquete PKG_SGC_TC_ALGODON.
    /// </summary>
    public class ResultadoTcAlgodonDto
    {
        public bool Exito { get; set; }
        public int? IdCert { get; set; }
        public string? MensajeError { get; set; }
    }
}
