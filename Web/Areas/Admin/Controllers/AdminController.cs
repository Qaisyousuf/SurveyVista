using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Model;
using Services.Interaces;
using Web.ViewModel.DashboardVM;

namespace Web.Areas.Admin.Controllers
{


    [Authorize(Roles ="Admin")]
    public class AdminController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDashboardRepository _dashboard;

        public AdminController(SignInManager<ApplicationUser> signInManager,IDashboardRepository dashboard)
        {
            _signInManager = signInManager;
            _dashboard = dashboard;
        }
        public async Task<IActionResult> Index()
        {
            var modelCounts = await _dashboard.GetModelCountsAsync();
            var bannerSelections = await _dashboard.GetCurrentBannerSelectionsAsync();
            var footerSelections = await _dashboard.GetCurrentFooterSelectionsAsync();

            var viewModel = new DashboardViewModel
            {
                ModelCounts = modelCounts,
                BannerSelections = bannerSelections,
                FooterSelections = footerSelections
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account", new { area = "" }); // Redirect to frontend login page
        }
    }
}
