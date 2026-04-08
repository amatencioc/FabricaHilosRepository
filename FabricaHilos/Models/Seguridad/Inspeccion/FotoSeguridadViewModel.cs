using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FabricaHilos.Models.Seguridad.Inspeccion
{
    public class FotoSeguridadViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un responsable de área.")]
        [Display(Name = "Responsable de Área")]
        public string ResponsableArea { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar un responsable de inspección.")]
        [Display(Name = "Responsable de Inspección")]
        public string ResponsableInspeccion { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar un centro de costo.")]
        [Display(Name = "Centro de Costo")]
        public string CentroCosto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar el tipo de inspección.")]
        [Display(Name = "Tipo de Inspección")]
        public string TipoInspeccion { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe ingresar la ubicación del hallazgo.")]
        [StringLength(100, ErrorMessage = "La ubicación no puede exceder los 100 caracteres.")]
        [Display(Name = "Ubicación del Hallazgo")]
        public string UbicacionFoto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar una imagen.")]
        [Display(Name = "Foto a subir")]
        public IFormFile Foto { get; set; } = null!;
    }

    public class AccionCorrectivaViewModel
    {
        [Required(ErrorMessage = "Debe ingresar la ubicación de la acción correctiva.")]
        [StringLength(100, ErrorMessage = "La ubicación no puede exceder los 100 caracteres.")]
        [Display(Name = "Ubicación de la Acción Correctiva")]
        public string UbicacionFoto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar una imagen.")]
        [Display(Name = "Foto de Acción Correctiva")]
        public IFormFile Foto { get; set; } = null!;
    }
}
