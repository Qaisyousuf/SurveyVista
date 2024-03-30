using Microsoft.AspNetCore.Mvc;
using Services.Interaces;

namespace Web.ViewComponents
{
    public class BannerViewComponent:ViewComponent
    {
       
        private readonly IBannerRepository _bannerRepository;

        public BannerViewComponent(IBannerRepository bannerRepository)
        {
           
            _bannerRepository = bannerRepository;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int bannerId = (int)TempData["bannerId"];
            var Banner = await _bannerRepository.GetBannerByIdAsync(bannerId);

            return View(Banner);
        }
    }
}
