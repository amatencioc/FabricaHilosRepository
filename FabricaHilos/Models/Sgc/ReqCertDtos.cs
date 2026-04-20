namespace FabricaHilos.Models.Sgc
{
    /// <summary>
    /// DTO para la tabla SIG.REQ_CERT (Requerimiento de Certificado)
    /// </summary>
    public class ReqCertDto
    {
        public int NumReq { get; set; }
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
    /// DTO para órdenes de compra asociadas a un requerimiento de certificado
    /// </summary>
    public class ReqCertOrdenCompraDto
    {
        public string? OrdenCompra { get; set; }
    }
}
