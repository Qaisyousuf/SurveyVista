using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Areas.Admin.Controllers
{

  
    public class RegisterController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
