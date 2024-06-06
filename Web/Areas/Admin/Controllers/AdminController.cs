using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Model;
using Services.Interaces;
using System.Security.Claims;
using Web.ViewModel.DashboardVM;

namespace Web.Areas.Admin.Controllers
{


    [Authorize(Roles ="Admin")]
    public class AdminController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDashboardRepository _dashboard;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(SignInManager<ApplicationUser> signInManager,IDashboardRepository dashboard, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _dashboard = dashboard;
            _userManager = userManager;
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
                FooterSelections = footerSelections,
                PerformanceData = new List<PerformanceDataViewModel>(),
                VisitorData = new List<VisitorDataViewModel>() // Initialize the new property
            };

            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    viewModel.FirstName = user.FirstName;
                    viewModel.LastName = user.LastName;
                }
            }
            else
            {
                viewModel.FirstName = "Guest";
                viewModel.LastName = string.Empty;
            }
            return View(viewModel);
        }


        [HttpGet]
        public JsonResult GetVisitorData()
        {
            var visitorData = new List<VisitorDataViewModel>
        {
            new VisitorDataViewModel { Time = DateTime.Now.ToString("HH:mm:ss"), VisitorCount = new Random().Next(0, 500) },
            new VisitorDataViewModel { Time = DateTime.Now.AddSeconds(-5).ToString("HH:mm:ss"), VisitorCount = new Random().Next(0, 500) },
            new VisitorDataViewModel { Time = DateTime.Now.AddSeconds(-10).ToString("HH:mm:ss"), VisitorCount = new Random().Next(0, 500) }
        };

            return Json(visitorData);
        }

        [HttpGet]
        public JsonResult GetPerformanceData()
        {
            var performanceData = new List<PerformanceDataViewModel>
        {
            new PerformanceDataViewModel { Time = DateTime.Now.ToString("HH:mm:ss"), CPUUsage = new Random().Next(0, 100), MemoryUsage = new Random().Next(0, 100) },
            new PerformanceDataViewModel { Time = DateTime.Now.AddSeconds(-5).ToString("HH:mm:ss"), CPUUsage = new Random().Next(0, 100), MemoryUsage = new Random().Next(0, 100) },
            new PerformanceDataViewModel { Time = DateTime.Now.AddSeconds(-10).ToString("HH:mm:ss"), CPUUsage = new Random().Next(0, 100), MemoryUsage = new Random().Next(0, 100) }
        };

            return Json(performanceData);
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
