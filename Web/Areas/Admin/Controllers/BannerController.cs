using Microsoft.AspNetCore.Mvc;
using Model;
using Services.Interaces;
using System.Collections.Immutable;
using Web.ViewModel;

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
            var bannerFromdb = _banner.GetAllBanners();
            var viewmodel = new List<BannerViewModel>();

            foreach(var Banner in bannerFromdb)
            {
                viewmodel.Add(new BannerViewModel
                {
                    Id = Banner.Id,
                    Title = Banner.Title,
                    Content=Banner.Content,
                    Description=Banner.Description,
                    ImageUrl=Banner.ImageUrl,
                    LinkUrl=Banner.LinkUrl,

                });
            }
            
            return View(viewmodel);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Create(BannerViewModel viewmodel)
        {
            if(ModelState.IsValid)
            {

                var banner = new Banner
                {
                    
                    Title = viewmodel.Title,
                    Content = viewmodel.Content,
                    Description = viewmodel.Description,
                    LinkUrl = viewmodel.LinkUrl,
                    ImageUrl = viewmodel.ImageUrl,
                };

                await _banner.Add(banner);

                await _banner.commitAsync();

                return RedirectToAction(nameof(Index));
                
            }
            return View(viewmodel);
        }
    }
}
