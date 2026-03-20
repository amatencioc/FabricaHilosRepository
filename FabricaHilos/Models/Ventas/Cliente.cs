using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.Ventas
{
    public class Cliente
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [Display(Name = "Nombre / Razón Social")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Display(Name = "RUC / DNI")]
        [StringLength(20)]
        public string? RucDni { get; set; }

        [Display(Name = "Dirección")]
        [StringLength(200)]
        public string? Direccion { get; set; }

        [Display(Name = "Teléfono")]
        [StringLength(20)]
        public string? Telefono { get; set; }

        [Display(Name = "Correo Electrónico")]
        [EmailAddress]
        [StringLength(100)]
        public string? Correo { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    }
}
