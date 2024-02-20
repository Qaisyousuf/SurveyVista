using Microsoft.AspNetCore.Mvc;
using Services.Interaces;

namespace Web.Areas.Admin.Controllers
{
    public class BannerController : Controller
    {
        private readonly IBannerRepository _banner;

        public BannerController(IBannerRepository banner)
        {
            _banner = banner;
        }
        public IActionResult Index()
        {

            var baner = _banner.GetBanners();
            return View(baner);
        }
    }
}
