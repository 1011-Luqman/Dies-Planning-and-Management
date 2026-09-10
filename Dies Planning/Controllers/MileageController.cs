using Microsoft.AspNetCore.Mvc;

namespace Dies_Planning.Controllers
{
    public class MileageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
