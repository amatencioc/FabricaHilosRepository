using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.Ventas
{
    public enum EstadoPedido
    {
        Pendiente,
        Entregado,
        Cancelado
    }

    public class Pedido
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Número de Pedido")]
        [StringLength(20)]
        public string NumeroPedido { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Cliente")]
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        [Required]
        [Display(Name = "Fecha")]
        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; }

        [Display(Name = "Descripción / Productos")]
        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Required]
        [Display(Name = "Total")]
        [Range(0, double.MaxValue)]
        [DataType(DataType.Currency)]
        public decimal Total { get; set; }

        [Display(Name = "Estado")]
        public EstadoPedido Estado { get; set; } = EstadoPedido.Pendiente;

        [Display(Name = "Observaciones")]
        [StringLength(500)]
        public string? Observaciones { get; set; }
    }
}
