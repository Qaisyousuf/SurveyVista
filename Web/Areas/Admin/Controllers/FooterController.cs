using Data;
using Microsoft.AspNetCore.Components.Forms.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Interaces;
using Web.ViewModel.FooterVm;
using Web.ViewModel.SocialMediaVM;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Web.Areas.Admin.Controllers
{
    public class FooterController : Controller
    {
        private readonly IFooterRepository _footer;
        private readonly ISocialMediaRepository _socialMedia;
        private readonly SurveyContext _context;

        public FooterController(IFooterRepository footer,ISocialMediaRepository socialMedia,SurveyContext context)
        {
            _footer = footer;
            _socialMedia = socialMedia;
            _context = context;
        }
        public IActionResult Index()
        {

            //var footers = _footer.GetFooter();
            //var footers = _context.Footers.Include(f => f.FooterSocialMedias)
            //                          .ThenInclude(fsm => fsm.SocialMedia)
            //                          .ToList();

            var socialId = _context.FooterSocialMedias.Select(x => x.SocialId).ToList();

            var footer = _footer.GetFooterWithFooterSocial();

            var footerViewModels = footer.Select(footer => new ShowViewModel
            {
                Id = footer.Id,
                Title=footer.Title,
                Name=footer.Name,
                Owner=footer.Owner,
                CreatedBy=footer.CreatedBy,
                // Map other properties from Footer to FooterViewModel as needed
                SelectedSocialMediaIds = footer.FooterSocialMedias.Select(fsm => fsm.SocialId).ToList(),
              
                SocialMediaOptions = _context.SocialMedia
                    
                    .Select(sm => new SelectListItem
                    {
                        Value = sm.Id.ToString(),
                        Text = sm.Name
                    })
                    .ToList()
            }).ToList();

            return View(footerViewModels);
        }

        [HttpGet]
        public IActionResult Create()
        {




            var socialMediaOptions = _socialMedia.GetSocialMedia()
           .Select(sm => new SelectListItem
           {
               Value = sm.Id.ToString(),
               Text = sm.Name // Adjust this based on your SocialMedia properties
           })
           .ToList();

            var viewModel = new InserFooterViewModel
            {
                SocialMediaOptions = socialMediaOptions
            };

            return View(viewModel);
           
           
        }

        [HttpPost]
        public async Task<IActionResult> Create(InserFooterViewModel viewmodel)
        {




            if (ModelState.IsValid)
            {
                var footer = new Footer
                {

                    Id = viewmodel.Id,
                    Name = viewmodel.Name,
                    Title = viewmodel.Title,
                    Owner = viewmodel.Owner,
                    Content = viewmodel.Content,
                    CreatedBy = viewmodel.CreatedBy,
                    UpdatedBy = viewmodel.UpdatedBy,
                    LastUpdated = viewmodel.LastUpdated,
                    ImageUlr = viewmodel.ImageUlr,
                    Sitecopyright = viewmodel.Sitecopyright,
                };

                if (viewmodel.SelectedSocialMediaIds != null)
                {
                    footer.FooterSocialMedias = viewmodel.SelectedSocialMediaIds
                        .Select(id => new FooterSocialMedia
                        {
                            SocialId = id
                        })
                        .ToList();
                }

                await  _footer.Add(footer);
                await _footer.commitAsync();
                return RedirectToAction("Index"); // Redirect to appropriate action
            }

            // If ModelState is not valid, re-populate options and return to the view
            viewmodel.SocialMediaOptions = _socialMedia.GetSocialMedia()
                .Select(sm => new SelectListItem
                {
                    Value = sm.Id.ToString(),
                    Text = sm.Name // Adjust this based on your SocialMedia properties
                })
                .ToList();

            return View(viewmodel);


        }

        private List<CheckBoxViewModel> GetsocialMdeia()
        {
            var socialMedia = _socialMedia.GetSocialMedia();
                

            List<CheckBoxViewModel> selectListItems = new List<CheckBoxViewModel>();

            foreach (var item in socialMedia)
            {
                selectListItems.Add(new CheckBoxViewModel
                {
                    SocialMediaName=item.Name,
                    SocialMediaId=item.Id,
                    IsSelected=false,
                });
            }

            return selectListItems;
        }
    }
}
