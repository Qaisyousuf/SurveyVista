using Microsoft.AspNetCore.Mvc;
using Services.Interaces;
using System.Diagnostics;
using Web.Models;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IPageRepository _pageRepository;

        public HomeController(IPageRepository pageRepository)
        {
            _pageRepository = pageRepository;
        }

        public IActionResult Index(string slug)
        {
            if (string.IsNullOrEmpty(slug))
                slug = "home";

            if (!_pageRepository.SlugExists(slug))
                return RedirectToAction(nameof(Error));

            var pageFromdb = _pageRepository.GetPageSlug(slug);

            TempData["bannerId"] = pageFromdb.BannerId;
            TempData["Footer"] = pageFromdb.FooterId;
            

            return View(pageFromdb);
        }

     

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
