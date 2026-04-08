using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.Seguridad.Inspeccion
{
    public class AccionCorrectivaViewModel
    {
        [Required(ErrorMessage = "Debe proporcionar la ubicación de la acción correctiva.")]
        [StringLength(100, ErrorMessage = "La ubicación no puede exceder los 100 caracteres.")]
        [Display(Name = "Ubicación de la Acción Correctiva")]
        public string UbicacionFoto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe adjuntar la foto de la acción correctiva.")]
        [Display(Name = "Foto de la Acción Correctiva")]
        public IFormFile? Foto { get; set; }
    }
}
