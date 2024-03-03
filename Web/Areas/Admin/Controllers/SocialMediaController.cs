using Microsoft.AspNetCore.Mvc;
using Model;
using Services.Interaces;
using Web.ViewModel.SocialMediaVM;

namespace Web.Areas.Admin.Controllers
{
    public class SocialMediaController : Controller
    {
        private readonly ISocialMediaRepository _context;

        public SocialMediaController(ISocialMediaRepository context)
        {
            _context = context;
        }
        public IActionResult Index()
        {

            var socialMediaFromdb = _context.GetSocialMedia();


            var viewmodel = new List<SocialMediaViewModel>();

            foreach (var item in socialMediaFromdb)
            {
                viewmodel.Add(new SocialMediaViewModel
                {
                    Id = item.Id,
                    Name = item.Name,
                    Url = item.Url,
                  
                }) ;
            }

            return View(viewmodel);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(SocialMediaViewModel model)
        {
            if(ModelState.IsValid)
            {
                var socialMedia = new SocialMedia
                {
                    Id=model.Id,
                    Name=model.Name,
                    Url=model.Url,
                };

                await _context.Add(socialMedia);

                await _context.commitAsync();
                TempData["Success"] = "Social media created successfully";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
    }
}
