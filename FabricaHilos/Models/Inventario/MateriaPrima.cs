using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.Inventario
{
    public class MateriaPrima
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [Display(Name = "Nombre")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo es obligatorio")]
        [Display(Name = "Tipo")]
        [StringLength(50)]
        public string Tipo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La unidad de medida es obligatoria")]
        [Display(Name = "Unidad de Medida")]
        [StringLength(20)]
        public string UnidadMedida { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Cantidad Disponible")]
        [Range(0, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor o igual a 0")]
        public decimal CantidadDisponible { get; set; }

        [Required]
        [Display(Name = "Stock Mínimo")]
        [Range(0, double.MaxValue)]
        public decimal StockMinimo { get; set; }

        [Display(Name = "Proveedor")]
        [StringLength(100)]
        public string? Proveedor { get; set; }

        [Display(Name = "Fecha de Último Ingreso")]
        [DataType(DataType.Date)]
        public DateTime? FechaUltimoIngreso { get; set; }

        [Display(Name = "Observaciones")]
        [StringLength(500)]
        public string? Observaciones { get; set; }

        public bool StockBajo => CantidadDisponible < StockMinimo;
    }
}
