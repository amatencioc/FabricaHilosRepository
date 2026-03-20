using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabricaHilos.Controllers
{
    [Authorize]
    public class ProduccionController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
