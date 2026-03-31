using FabricaHilos.Models.Seguridad;
using FabricaHilos.Services.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class SeguridadController : Controller
    {
        private readonly ProcesadorImagenSeguridad _procesadorImagen;
        private readonly ILogger<SeguridadController> _logger;

        public SeguridadController(IConfiguration configuration, ILogger<SeguridadController> logger)
        {
            var rutaSeguridad = configuration.GetValue<string>("RutaSeguridad")
                ?? throw new InvalidOperationException(
                    "La clave 'RutaSeguridad' no está definida en appsettings.json.");

            _procesadorImagen = new ProcesadorImagenSeguridad(rutaSeguridad);
            _logger = logger;
        }

        [HttpGet]
        public IActionResult SubirFoto()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubirFoto(FotoSeguridadViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var nombreArchivo = await _procesadorImagen.GuardarYOptimizarImagenAsync(model.Foto);
                ViewBag.Mensaje = $"Imagen subida correctamente: {nombreArchivo}";
                _logger.LogInformation("Imagen de seguridad guardada: {NombreArchivo}", nombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar imagen de seguridad");
                ViewBag.Error = $"Error al subir imagen: {ex.Message}";
            }

            return View(new FotoSeguridadViewModel());
        }
    }
}
