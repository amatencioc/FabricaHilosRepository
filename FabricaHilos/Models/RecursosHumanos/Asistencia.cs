using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.RecursosHumanos
{
    public enum EstadoAsistencia
    {
        Presente,
        Falta,
        Tardanza,
        PermisoJustificado
    }

    public class Asistencia
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Empleado")]
        public int EmpleadoId { get; set; }
        public Empleado? Empleado { get; set; }

        [Required]
        [Display(Name = "Fecha")]
        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; }

        [Display(Name = "Hora de Entrada")]
        [DataType(DataType.Time)]
        public TimeSpan? HoraEntrada { get; set; }

        [Display(Name = "Hora de Salida")]
        [DataType(DataType.Time)]
        public TimeSpan? HoraSalida { get; set; }

        [Display(Name = "Estado")]
        public EstadoAsistencia Estado { get; set; } = EstadoAsistencia.Presente;

        [Display(Name = "Observaciones")]
        [StringLength(300)]
        public string? Observaciones { get; set; }
    }
}
