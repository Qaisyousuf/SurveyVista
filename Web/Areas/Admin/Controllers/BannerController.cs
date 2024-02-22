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
                TempData["Success"] = "Banner created successfully";

                return RedirectToAction(nameof(Index));
                
            }
            return View(viewmodel);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var bannerFromdb = _banner.GetBannerById(id);

            var viewmodel = new BannerViewModel
            {
                Id=bannerFromdb.Id,
                Title=bannerFromdb.Title,
                Description=bannerFromdb.Description,
                Content=bannerFromdb.Content,
                ImageUrl=bannerFromdb.ImageUrl,
                LinkUrl=bannerFromdb.ImageUrl,
            };

            return View(viewmodel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(BannerViewModel viewmodel)
        {
            if(ModelState.IsValid)
            {
                var banner = _banner.GetBannerById(viewmodel.Id);

                banner.Title = viewmodel.Title;
                banner.Content = viewmodel.Content;
                banner.Description = viewmodel.Description;
                banner.LinkUrl = viewmodel.LinkUrl;
                banner.ImageUrl = viewmodel.ImageUrl;

                _banner.Update(banner);

                await _banner.commitAsync();
                TempData["Success"] = "Banner updated successfully";

                return RedirectToAction(nameof(Index));

            }

              
            

            return View(viewmodel);
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var bannerFromDb = _banner.GetBannerById(id);

            var viewmodel = new BannerViewModel
            {
                Id=bannerFromDb.Id,
                Title=bannerFromDb.Title,
                Description=bannerFromDb.Description,
                Content=bannerFromDb.Content,
                ImageUrl=bannerFromDb.ImageUrl,
                LinkUrl=bannerFromDb.ImageUrl,

            };

            return View(viewmodel);
        }

        [HttpPost]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirm(int id)
        {

            //var banner = _banner.GetBannerById(id);

            _banner.Delete(id);

            await _banner.commitAsync();
            TempData["Success"] = "Banner deleted successfully";

            return RedirectToAction(nameof(Index));

           

          
        }
    }
}
