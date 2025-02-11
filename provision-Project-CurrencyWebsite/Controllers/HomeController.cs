using Microsoft.AspNetCore.Mvc;

namespace CurrencyWebsite.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}