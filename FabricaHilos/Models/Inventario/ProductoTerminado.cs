using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.Inventario
{
    public class ProductoTerminado
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [Display(Name = "Nombre del Hilo")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Tipo")]
        [StringLength(50)]
        public string Tipo { get; set; } = string.Empty;

        [Display(Name = "Color")]
        [StringLength(50)]
        public string? Color { get; set; }

        [Display(Name = "Calibre")]
        [StringLength(30)]
        public string? Calibre { get; set; }

        [Required]
        [Display(Name = "Unidad")]
        [StringLength(20)]
        public string Unidad { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Cantidad")]
        [Range(0, double.MaxValue)]
        public decimal Cantidad { get; set; }

        [Required]
        [Display(Name = "Precio Unitario")]
        [Range(0, double.MaxValue)]
        [DataType(DataType.Currency)]
        public decimal PrecioUnitario { get; set; }

        [Display(Name = "Descripción")]
        [StringLength(500)]
        public string? Descripcion { get; set; }
    }
}
