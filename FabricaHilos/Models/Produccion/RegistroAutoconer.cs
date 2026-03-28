using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FabricaHilos.Models.Produccion
{
    public class RegistroAutoconer
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El número de Autoconer es obligatorio.")]
        [Display(Name = "Autoconer N°")]
        [StringLength(50)]
        public string NumeroAutoconer { get; set; } = string.Empty;

        [Required(ErrorMessage = "El código de operador es obligatorio.")]
        [Display(Name = "Operador N°")]
        [StringLength(20)]
        public string CodigoOperador { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        [Display(Name = "Fecha")]
        [DataType(DataType.DateTime)]
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "El turno es obligatorio.")]
        [Display(Name = "Turno")]
        [StringLength(10)]
        public string Turno { get; set; } = string.Empty;

        [Display(Name = "Código de Receta")]
        [StringLength(20)]
        public string? CodigoReceta { get; set; }

        [Required(ErrorMessage = "El lote es obligatorio.")]
        [Display(Name = "Lote / Partida")]
        [StringLength(50)]
        public string Lote { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción de material es obligatoria.")]
        [Display(Name = "Material")]
        [StringLength(100)]
        public string DescripcionMaterial { get; set; } = string.Empty;

        [Display(Name = "Color")]
        [StringLength(50)]
        public string? Color { get; set; }

        [Display(Name = "V (m/min)")]
        [Range(0, double.MaxValue)]
        public decimal? VelocidadMMin { get; set; }

        [Display(Name = "HI (Husos Inactivos)")]
        [Range(0, int.MaxValue)]
        public int? HusosInactivos { get; set; }

        [Display(Name = "H Inicio")]
        [DataType(DataType.DateTime)]
        public DateTime? HoraInicio { get; set; }

        [Display(Name = "H Final")]
        [DataType(DataType.DateTime)]
        public DateTime? HoraFinal { get; set; }

        [Display(Name = "Bloque")]
        public int? Bloque { get; set; }

        [Required(ErrorMessage = "El título es obligatorio.")]
        [Display(Name = "Título")]
        [StringLength(100)]
        public string Titulo { get; set; } = string.Empty;

        [Display(Name = "P. Bruto")]
        [Range(0, double.MaxValue)]
        public decimal? PesoBruto { get; set; }

        [Display(Name = "Q (Cantidad)")]
        [Range(0, int.MaxValue)]
        public int? Cantidad { get; set; }

        [Display(Name = "Puntaje")]
        [Range(0, int.MaxValue)]
        public int? Puntaje { get; set; }

        // Tramos 1-6
        [Display(Name = "Tramo 1")]
        public bool Tramo1 { get; set; }

        [Display(Name = "Tramo 2")]
        public bool Tramo2 { get; set; }

        [Display(Name = "Tramo 3")]
        public bool Tramo3 { get; set; }

        [Display(Name = "Tramo 4")]
        public bool Tramo4 { get; set; }

        [Display(Name = "Tramo 5")]
        public bool Tramo5 { get; set; }

        [Display(Name = "Tramo 6")]
        public bool Tramo6 { get; set; }

        [Display(Name = "Destino")]
        [StringLength(10)]
        public string? Destino { get; set; }

        [Display(Name = "Cliente")]
        [StringLength(100)]
        public string? Cliente { get; set; }

        [Display(Name = "Reproceso")]
        public bool Reproceso { get; set; }

        [Display(Name = "Motivo de Paralización")]
        [StringLength(200)]
        public string? MotivoParalizacion { get; set; }

        [Display(Name = "Estado")]
        public EstadoOrden Estado { get; set; } = EstadoOrden.EnProceso;

        [Display(Name = "Cerrado")]
        public bool Cerrado { get; set; }
    }
}
