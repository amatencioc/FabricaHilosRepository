using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.Seguridad
{
    public class FotoSeguridadViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar una imagen.")]
        [Display(Name = "Foto a subir")]
        public IFormFile Foto { get; set; } = null!;
    }
}
