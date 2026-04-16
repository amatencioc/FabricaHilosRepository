using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using FabricaHilos.Attributes;

namespace FabricaHilos.Models.Seguridad.Inspeccion
{
    /// <summary>Registrar Inspección (sin foto/ubicación; con objetivo).</summary>
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

        [Required(ErrorMessage = "Debe ingresar el objetivo del hallazgo.")]
        [StringLength(200, ErrorMessage = "El objetivo no puede exceder los 200 caracteres.")]
        [Display(Name = "Objetivo del Hallazgo")]
        public string ObjetivoHallazgo { get; set; } = string.Empty;
    }

    public class AccionCorrectivaViewModel
    {
        [Required(ErrorMessage = "Debe ingresar la ubicación de la acción correctiva.")]
        [StringLength(100, ErrorMessage = "La ubicación no puede exceder los 100 caracteres.")]
        [Display(Name = "Ubicación de la Acción Correctiva")]
        public string UbicacionFoto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar una imagen.")]
        [AllowedFile(10, "image/jpeg", "image/png", "image/webp")]
        [Display(Name = "Foto de Acción Correctiva")]
        public IFormFile Foto { get; set; } = null!;
    }

    /// <summary>Editar Inspección (mismos campos que registrar).</summary>
    public class EditarHallazgoViewModel
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

        [Required(ErrorMessage = "Debe ingresar el objetivo del hallazgo.")]
        [StringLength(200, ErrorMessage = "El objetivo no puede exceder los 200 caracteres.")]
        [Display(Name = "Objetivo del Hallazgo")]
        public string ObjetivoHallazgo { get; set; } = string.Empty;
    }

    /// <summary>Agregar hallazgo desde la ventana H / AC (ubicación + foto).</summary>
    public class AgregarHallazgoFotoViewModel
    {
        [Display(Name = "Ubicación del Hallazgo")]
        [StringLength(100, ErrorMessage = "La ubicación no puede exceder los 100 caracteres.")]
        public string UbicacionFoto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar una imagen.")]
        [AllowedFile(10, "image/jpeg", "image/png", "image/webp")]
        [Display(Name = "Foto del Hallazgo")]
        public IFormFile Foto { get; set; } = null!;
    }
}
