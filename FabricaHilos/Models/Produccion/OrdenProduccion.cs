using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FabricaHilos.Models.Produccion
{
    public enum EstadoOrden
    {
        EnProceso = 1,
        Terminado = 3,
        Anulado   = 9
    }

    public class OrdenProduccion : IValidatableObject
    {
        public int Id { get; set; }

        // RECETA: campo opcional. [Column] preserva el nombre de columna en BD.
        [Column("NumeroOrden")]
        [Display(Name = "Código de Receta")]
        [StringLength(20)]
        public string? CodigoReceta { get; set; }

        [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        [Display(Name = "Fecha de Inicio")]
        [DataType(DataType.DateTime)]
        public DateTime FechaInicio { get; set; }

        [Column("TipoHilo")]
        [Required(ErrorMessage = "La descripción del material es obligatoria.")]
        [Display(Name = "Descripción de Material")]
        [StringLength(100)]
        public string DescripcionMaterial { get; set; } = string.Empty;

        [Display(Name = "Estado")]
        public EstadoOrden Estado { get; set; } = EstadoOrden.EnProceso;

        [Required(ErrorMessage = "El campo Operario es obligatorio.")]
        [Display(Name = "Código de Operario")]
        [StringLength(20)]
        public string? EmpleadoId { get; set; }

        [Required(ErrorMessage = "El campo Lote es obligatorio.")]
        [Display(Name = "Lote")]
        [StringLength(50)]
        public string? Lote { get; set; }

        [Required(ErrorMessage = "El campo Tipo de Máquina es obligatorio.")]
        [Display(Name = "Código de Máquina")]
        [StringLength(50)]
        public string? CodigoMaquina { get; set; }

        [Required(ErrorMessage = "El campo Máquina es obligatorio.")]
        [Display(Name = "Máquina")]
        [StringLength(100)]
        public string? Maquina { get; set; }

        [Required(ErrorMessage = "El campo Título es obligatorio.")]
        [Display(Name = "Título")]
        [StringLength(100)]
        public string? Titulo { get; set; }

        [Required(ErrorMessage = "El campo Turno es obligatorio.")]
        [Display(Name = "Turno")]
        [StringLength(50)]
        public string? Turno { get; set; }

        [Column("Paso")]
        [Display(Name = "Paso")]
        [StringLength(100)]
        public string? PasoManuar { get; set; }

        [Display(Name = "Cerrado")]
        public bool Cerrado { get; set; } = false;

        // Campos de Detalle de Producción
        [Display(Name = "Velocidad")]
        [Range(0, double.MaxValue)]
        public decimal? Velocidad { get; set; }

        [Display(Name = "Metraje")]
        [Range(0, double.MaxValue)]
        public decimal? Metraje { get; set; }

        [Display(Name = "Rollo Tacho")]
        public int? RolloTacho { get; set; }

        [Display(Name = "Kg Neto")]
        [Range(0, double.MaxValue)]
        public decimal? KgNeto { get; set; }

        [Display(Name = "Produc Teorico")]
            [Range(0, double.MaxValue)]
            public decimal? ProducTeorico { get; set; }

            [Display(Name = "Eficienc Teorico")]
            [Range(0, 100)]
            public decimal? EficiencTeorico { get; set; }

            // Campos Pabileras
            [Display(Name = "Contador Inicial")]
            [Range(0, double.MaxValue)]
            public decimal? ContadorInicial { get; set; }

            [Display(Name = "Husos Inactivas (HI)")]
            [Range(0, double.MaxValue)]
            public decimal? HorasInactivas { get; set; }

            [Display(Name = "Contador Final")]
            [Range(0, double.MaxValue)]
            public decimal? ContadorFinal { get; set; }

            [Display(Name = "Nro. Parada")]
            public int? NroParada { get; set; }

            [Display(Name = "Fecha Final")]
            [DataType(DataType.DateTime)]
            public DateTime? FechaFin { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (CodigoMaquina == "M")
                {
                    if (string.IsNullOrWhiteSpace(PasoManuar))
                        yield return new ValidationResult(
                            "El campo Paso Manuar es obligatorio.",
                            new[] { nameof(PasoManuar) });
                }
                else if (CodigoMaquina == "P")
                {
                    if (!ContadorInicial.HasValue)
                        yield return new ValidationResult(
                            "El campo Contador Inicial es obligatorio.",
                            new[] { nameof(ContadorInicial) });

                    if (!HorasInactivas.HasValue)
                        yield return new ValidationResult(
                            "El campo Husos Inactivas (HI) es obligatorio.",
                            new[] { nameof(HorasInactivas) });
                }
            }
        }
}
