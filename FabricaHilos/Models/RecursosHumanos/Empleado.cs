using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.RecursosHumanos
{
    public class Empleado
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [Display(Name = "Nombre Completo")]
        [StringLength(150)]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required]
        [Display(Name = "DNI")]
        [StringLength(20)]
        public string Dni { get; set; } = string.Empty;

        [Display(Name = "Cargo")]
        [StringLength(100)]
        public string? Cargo { get; set; }

        [Display(Name = "Área")]
        [StringLength(100)]
        public string? Area { get; set; }

        [Required]
        [Display(Name = "Fecha de Ingreso")]
        [DataType(DataType.Date)]
        public DateTime FechaIngreso { get; set; }

        [Display(Name = "Salario")]
        [Range(0, double.MaxValue)]
        [DataType(DataType.Currency)]
        public decimal Salario { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        [Display(Name = "Teléfono")]
        [StringLength(20)]
        public string? Telefono { get; set; }

        [Display(Name = "Correo")]
        [EmailAddress]
        [StringLength(100)]
        public string? Correo { get; set; }

        public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
    }
}
