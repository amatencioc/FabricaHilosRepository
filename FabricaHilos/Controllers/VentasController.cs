using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FabricaHilos.Filters;

namespace FabricaHilos.Controllers
{
    [Authorize]
    [VentasAuthorize]
    public class VentasController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "ConsultaTc");
        }
    }
}
