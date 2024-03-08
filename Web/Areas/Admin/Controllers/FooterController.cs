using Data;
using Microsoft.AspNetCore.Components.Forms.Mapping;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Implemnetation;
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
                TempData["Success"] = "Footer created successfully";
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

        [HttpGet]
        public IActionResult Edit(int id)
        {

            var footer = _footer.GetFooterByIdWithSocialMedia(id);

            var socialMediaOptions =_socialMedia.GetSocialMedia()
              .Select(sm => new SelectListItem
              {
                  Value = sm.Id.ToString(),
                  Text = sm.Name,
                  Selected = footer.FooterSocialMedias.Any(fsm => fsm.SocialId == sm.Id)
              })
              .ToList();

            var footerViewModel = new FooterEditViewModel
            {
                Id = footer.Id,
                Title = footer.Title,
                Name = footer.Name,
                Owner = footer.Owner,
                Content = footer.Content,
                CreatedBy = footer.CreatedBy,
                UpdatedBy = footer.UpdatedBy,
                LastUpdated = footer.LastUpdated,
                ImageUlr = footer.ImageUlr,
                Sitecopyright = footer.Sitecopyright,
                // Map other properties from Footer to FooterViewModel as needed
                SocialMediaOptions = socialMediaOptions,
                
                
                // Map other properties from Footer to FooterUpdateViewModel as needed
               
            };


            return View(footerViewModel);

         


        }

        [HttpPost]
        public async Task<IActionResult> Edit([Bind(include: "Id,Title,Name,Owner,Content,CreatedBy,UpdatedBy,LastUpdated,ImageUlr,Sitecopyright,SelectedSocialMediaIds,SocialMediaOptions")] FooterEditViewModel viewmodel)
        {
           


            if (ModelState.IsValid)
            {
                var footer = _footer.GetFooterByIdWithSocialMedia(viewmodel.Id);

                // Update other properties in the Footer model based on viewmodel

                footer.Id = viewmodel.Id;
                footer.Title = viewmodel.Title;
                footer.Owner = viewmodel.Owner;
                footer.Content = viewmodel.Content;
                footer.Name = viewmodel.Name;
                footer.LastUpdated = viewmodel.LastUpdated;
                footer.UpdatedBy = viewmodel.UpdatedBy;
                footer.CreatedBy = viewmodel.CreatedBy;
                footer.Sitecopyright = viewmodel.Sitecopyright;
                footer.ImageUlr = viewmodel.ImageUlr;

                // Clear existing associations and add selected ones
                //footer.FooterSocialMedias.Clear();



                var selectedSocialMediaIds = viewmodel.SelectedSocialMediaIds ?? new List<int>();

                var selectedSocialMedias = _socialMedia.GetSocialMedia()
                    .Where(sm => selectedSocialMediaIds.Contains(sm.Id))
                    .ToList();



                foreach (var socialMedia in selectedSocialMedias)
                {

                    footer.FooterSocialMedias.Add(new FooterSocialMedia
                    {
                        SocialId = socialMedia.Id
                        // Add other properties as needed
                    });
                }


                _footer.Update(footer);
                await _footer.commitAsync();
                TempData["Success"] = "Footer updated successfully";

                return RedirectToAction(nameof(Index));
            }


            return View(viewmodel);

        }

        [HttpGet]
        public IActionResult Delete(int id)
        {

            var footer = _footer.GetFooterByIdWithSocialMedia(id);

            var socialMediaOptions = _socialMedia.GetSocialMedia()
              .Select(sm => new SelectListItem
              {
                  Value = sm.Id.ToString(),
                  Text = sm.Name,
                  Selected = footer.FooterSocialMedias.Any(fsm => fsm.SocialId == sm.Id)
              })
              .ToList();

            var footerViewModel = new FooterDeleteViewModel
            {
                Id = footer.Id,
                Title = footer.Title,
                Name = footer.Name,
                Owner = footer.Owner,
                Content = footer.Content,
                CreatedBy = footer.CreatedBy,
                UpdatedBy = footer.UpdatedBy,
                LastUpdated = footer.LastUpdated,
                ImageUlr = footer.ImageUlr,
                Sitecopyright = footer.Sitecopyright,
                // Map other properties from Footer to FooterViewModel as needed
                SocialMediaOptions = socialMediaOptions,


                // Map other properties from Footer to FooterUpdateViewModel as needed

            };


            return View(footerViewModel);

        }

        [HttpPost]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirm(int id)
        {

            _footer.Delete(id);

            await _footer.commitAsync();
            TempData["Success"] = "Footer deleted successfully";

            return RedirectToAction(nameof(Index));

        }




    }
}
