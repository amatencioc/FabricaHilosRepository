using System.ComponentModel.DataAnnotations;

namespace LaColonial.Models;

public class ContactoForm
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Telefono { get; set; }

    [StringLength(200)]
    public string? Asunto { get; set; }

    [Required(ErrorMessage = "El mensaje es obligatorio")]
    [StringLength(2000, MinimumLength = 10)]
    public string Mensaje { get; set; } = string.Empty;
}
