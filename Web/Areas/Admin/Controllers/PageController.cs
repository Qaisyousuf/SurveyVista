using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Model;
using Services.Interaces;
using Services.SlugServices;
using Web.ViewModel.CmsVM;
using Web.ViewModel.PageVM;
using Microsoft.EntityFrameworkCore;

namespace Web.Areas.Admin.Controllers
{

    [Area("Admin")]
    public class PageController : Controller
    {
        private readonly IPageRepository _pageRepository;
        private readonly IBannerRepository _bannerRepository;
        private readonly IFooterRepository _footerRepository;
        private readonly SurveyContext _context;

        public PageController(IPageRepository pageRepository,IBannerRepository bannerRepository, IFooterRepository footerRepository, SurveyContext context)
        {
         _pageRepository = pageRepository;
         _bannerRepository = bannerRepository;
          _footerRepository = footerRepository;
            _context = context;
        }
        public IActionResult Index()
        {
            var pages = _pageRepository.GetPageWithAll();


            List<PageViewModel> result = new List<PageViewModel>();

            foreach (var page in pages)
            {
                result.Add(new PageViewModel { Id = page.Id, Title = page.Title, Slug = page.Slug, banner = page.banner,Footer=page.footer });
            }

            return View(result);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.DropDownData=GetSidebarsForDropDownList();
            ViewBag.FooterDropDown = GetFooterForDropDownList();

            return View();
        }




        [HttpPost]
        public async Task<IActionResult> Create(PageViewModel viewmodel)
        {
            if(!ModelState.IsValid)
            {
                ViewBag.DropDownData = GetSidebarsForDropDownList();
                ViewBag.FooterDropDown = GetFooterForDropDownList();
                return View(viewmodel);
            }

            string slug;

            if (string.IsNullOrEmpty(viewmodel.Slug))
                slug = SlugService.Create(true, viewmodel.Title);
            else
                slug = SlugService.Create(true, viewmodel.Slug);



            if(_pageRepository.SlugExists(slug))
            {
                ModelState.AddModelError("", "Title or slug exists");
                ViewBag.DropDownData = GetSidebarsForDropDownList();
                ViewBag.FooterDropDown = GetFooterForDropDownList();
                return View(viewmodel);
            }

            Page page = new Page();

            page.Title = viewmodel.Title;
            page.Slug = slug;
            page.Content = viewmodel.Content;
            page.banner = viewmodel.banner;
            page.BannerId = viewmodel.BannerId;
            page.footer = viewmodel.Footer;
            page.FooterId = viewmodel.FooterId;


            _pageRepository.Add(page);
           await _pageRepository.commitAsync();
            TempData["Success"] = "page created successfully";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var pageFromdb=_pageRepository.GetPageById(id);

            var viewmodel = new PageViewModel
            {
                Id = pageFromdb.Id,
                Title = pageFromdb.Title,
                Slug = pageFromdb.Slug,
                Content=pageFromdb.Content,
                banner=pageFromdb.banner,
                BannerId=pageFromdb.BannerId,
                Footer = pageFromdb.footer,
                FooterId = pageFromdb.FooterId,

            };
            ViewBag.DropDownData = GetSidebarsForDropDownList();
            ViewBag.FooterDropDown = GetFooterForDropDownList();


            return View(viewmodel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(PageViewModel viewmodel)
        {
            if(!ModelState.IsValid)
            {

                ViewBag.DropDownData = GetSidebarsForDropDownList();
                ViewBag.FooterDropDown = GetFooterForDropDownList();
                return View(viewmodel);

            }

            string slug;

            if (string.IsNullOrEmpty(viewmodel.Slug))
                slug = SlugService.Create(true, viewmodel.Title);
            else
                slug = SlugService.Create(true, viewmodel.Slug);

            if(_pageRepository.SlugExists(slug,viewmodel.Id))
            {

                ModelState.AddModelError("", "Title or slug exists");
                ViewBag.DropDownData = GetSidebarsForDropDownList();
                ViewBag.FooterDropDown = GetFooterForDropDownList();
                return View(viewmodel);
            }

            Page page = _pageRepository.GetPageById(viewmodel.Id);

            page.Title = viewmodel.Title;
            page.Slug = slug;
            page.Content = viewmodel.Content;
            page.banner = viewmodel.banner;
            page.BannerId = viewmodel.BannerId;
            page.footer = viewmodel.Footer;
            page.FooterId = viewmodel.FooterId;


            _pageRepository.Update(page);

            await _pageRepository.commitAsync();

            TempData["Success"] = "page updated successfully";


            return RedirectToAction(nameof(Index));

        }


        [HttpGet]
        public IActionResult Delete(int id)
        {
            var pageFromdb = _pageRepository.GetPageById(id);

            var viewmodel = new PageViewModel
            {
                Id = pageFromdb.Id,
                Title = pageFromdb.Title,
                Slug = pageFromdb.Slug,
                Content = pageFromdb.Content,
                banner = pageFromdb.banner,
                BannerId = pageFromdb.BannerId,
                Footer = pageFromdb.footer,
                FooterId = pageFromdb.FooterId,


            };
            ViewBag.DropDownData = GetSidebarsForDropDownList();
            ViewBag.FooterDropDown = GetFooterForDropDownList();


            return View(viewmodel);
        }


        [HttpPost]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirm(int id)
        {
           

            _pageRepository.Delete(id);
            await _pageRepository.commitAsync();
            TempData["Success"] = "page Deleted successfully";

            return RedirectToAction(nameof(Index));

        }
        private List<SelectListItem> GetSidebarsForDropDownList()
        {
             var banners = _bannerRepository.GetBannersForPage();

            List<SelectListItem> dropDown = new List<SelectListItem>();

            foreach (var item in banners)
            {
                dropDown.Add(new SelectListItem { Text = item.Title, Value = item.Id.ToString() });
            }

            return dropDown;
        }

        private List<SelectListItem> GetFooterForDropDownList()
        {
            var banners = _footerRepository.GetFooter();

            List<SelectListItem> dropDown = new List<SelectListItem>();

            foreach (var item in banners)
            {
                dropDown.Add(new SelectListItem { Text = item.Title, Value = item.Id.ToString() });
            }

            return dropDown;
        }


        public async Task<IActionResult> CmsDashboard()
        {
            var viewModel = new CmsDashboardViewModel
            {
                Pages = await _context.Pages
                    .Include(p => p.banner)
                    .Include(p => p.footer)
                    .OrderByDescending(p => p.Id)
                    .ToListAsync(),

                Banners = await _context.Banners
                    .OrderByDescending(b => b.Id)
                    .ToListAsync(),

                Footers = await _context.Footers
                    .Include(f => f.FooterSocialMedias)
                        .ThenInclude(fsm => fsm.SocialMedia)
                    .OrderByDescending(f => f.Id)
                    .ToListAsync(),

                SocialMedias = await _context.SocialMedia
                    .OrderByDescending(s => s.Id)
                    .ToListAsync(),

                Addresses = await _context.Addresss
                    .OrderByDescending(a => a.Id)
                    .ToListAsync(),

                // Dropdown options for Page create/edit
                BannerOptions = await _context.Banners
                    .Select(b => new BannerSelectItem { Id = b.Id, Title = b.Title ?? "" })
                    .ToListAsync(),

                FooterOptions = await _context.Footers
                    .Select(f => new FooterSelectItem { Id = f.Id, Name = f.Name ?? "" })
                    .ToListAsync(),

                SocialMediaOptions = await _context.SocialMedia
                    .Select(s => new SocialMediaSelectItem { Id = s.Id, Name = s.Name ?? "", Url = s.Url ?? "" })
                    .ToListAsync()
            };

            return View(viewModel);
        }


        // ═══════════════════════════════════════════════════════════
        // PAGE CRUD (AJAX)
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> GetPage(int id)
        {
            var page = await _context.Pages
                .Include(p => p.banner)
                .Include(p => p.footer)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (page == null) return Json(new { success = false, message = "Page not found." });

            return Json(new
            {
                success = true,
                data = new
                {
                    page.Id,
                    page.Title,
                    page.Slug,
                    page.Content,
                    page.BannerId,
                    page.FooterId,
                    BannerTitle = page.banner?.Title ?? "",
                    FooterName = page.footer?.Name ?? ""
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreatePageAjax(string title, string slug, string content, int bannerId, int footerId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(title))
                    return Json(new { success = false, message = "Title is required." });

                var page = new Page
                {
                    Title = title.Trim(),
                    Slug = string.IsNullOrWhiteSpace(slug) ? title.Trim().ToLower().Replace(" ", "-") : slug.Trim(),
                    Content = content?.Trim(),
                    BannerId = bannerId,
                    FooterId = footerId
                };

                _context.Pages.Add(page);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Page created successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePageAjax(int id, string title, string slug, string content, int bannerId, int footerId)
        {
            try
            {
                var page = await _context.Pages.FindAsync(id);
                if (page == null) return Json(new { success = false, message = "Page not found." });

                page.Title = title?.Trim();
                page.Slug = string.IsNullOrWhiteSpace(slug) ? title?.Trim().ToLower().Replace(" ", "-") : slug.Trim();
                page.Content = content?.Trim();
                page.BannerId = bannerId;
                page.FooterId = footerId;

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Page updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeletePageAjax(int id)
        {
            try
            {
                var page = await _context.Pages.FindAsync(id);
                if (page == null) return Json(new { success = false, message = "Page not found." });

                _context.Pages.Remove(page);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Page deleted." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }


        // ═══════════════════════════════════════════════════════════
        // BANNER CRUD (AJAX)
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> GetBanner(int id)
        {
            var b = await _context.Banners.FindAsync(id);
            if (b == null) return Json(new { success = false, message = "Banner not found." });

            return Json(new { success = true, data = new { b.Id, b.Title, b.Description, b.Content, b.LinkUrl, b.ImageUrl } });
        }

        [HttpPost]
        public async Task<IActionResult> CreateBannerAjax(string title, string description, string content, string linkUrl, string imageUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(title))
                    return Json(new { success = false, message = "Title is required." });

                var banner = new Banner
                {
                    Title = title.Trim(),
                    Description = description?.Trim(),
                    Content = content?.Trim(),
                    LinkUrl = linkUrl?.Trim(),
                    ImageUrl = imageUrl?.Trim()
                };

                _context.Banners.Add(banner);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Banner created successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBannerAjax(int id, string title, string description, string content, string linkUrl, string imageUrl)
        {
            try
            {
                var b = await _context.Banners.FindAsync(id);
                if (b == null) return Json(new { success = false, message = "Banner not found." });

                b.Title = title?.Trim();
                b.Description = description?.Trim();
                b.Content = content?.Trim();
                b.LinkUrl = linkUrl?.Trim();
                b.ImageUrl = imageUrl?.Trim();

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Banner updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBannerAjax(int id)
        {
            try
            {
                // Check if any page references this banner
                var inUse = await _context.Pages.AnyAsync(p => p.BannerId == id);
                if (inUse) return Json(new { success = false, message = "Cannot delete — this banner is used by one or more pages." });

                var b = await _context.Banners.FindAsync(id);
                if (b == null) return Json(new { success = false, message = "Banner not found." });

                _context.Banners.Remove(b);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Banner deleted." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }


        // ═══════════════════════════════════════════════════════════
        // FOOTER CRUD (AJAX)
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> GetFooter(int id)
        {
            var f = await _context.Footers
                .Include(x => x.FooterSocialMedias)
                    .ThenInclude(fsm => fsm.SocialMedia)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f == null) return Json(new { success = false, message = "Footer not found." });

            return Json(new
            {
                success = true,
                data = new
                {
                    f.Id,
                    f.Title,
                    f.Name,
                    f.Owner,
                    f.Content,
                    f.CreatedBy,
                    f.UpdatedBy,
                    f.ImageUlr,
                    f.Sitecopyright,
                    LastUpdated = f.LastUpdated.ToString("yyyy-MM-ddTHH:mm"),
                    SocialMediaIds = f.FooterSocialMedias?.Select(fsm => fsm.SocialId).ToList() ?? new List<int>()
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateFooterAjax(
            string title, string name, string owner, string content,
            string createdBy, string updatedBy, string imageUrl,
            string sitecopyright, int[] socialMediaIds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "Name is required." });

                var footer = new Footer
                {
                    Title = title?.Trim(),
                    Name = name.Trim(),
                    Owner = owner?.Trim(),
                    Content = content?.Trim(),
                    CreatedBy = createdBy?.Trim(),
                    UpdatedBy = updatedBy?.Trim(),
                    ImageUlr = imageUrl?.Trim(),
                    Sitecopyright = sitecopyright?.Trim(),
                    LastUpdated = DateTime.UtcNow
                };

                _context.Footers.Add(footer);
                await _context.SaveChangesAsync();

                // Add social media links
                if (socialMediaIds != null && socialMediaIds.Length > 0)
                {
                    foreach (var smId in socialMediaIds)
                    {
                        _context.FooterSocialMedias.Add(new FooterSocialMedia
                        {
                            FooterId = footer.Id,
                            SocialId = smId
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Footer created successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateFooterAjax(
            int id, string title, string name, string owner, string content,
            string createdBy, string updatedBy, string imageUrl,
            string sitecopyright, int[] socialMediaIds)
        {
            try
            {
                var f = await _context.Footers
                    .Include(x => x.FooterSocialMedias)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (f == null) return Json(new { success = false, message = "Footer not found." });

                f.Title = title?.Trim();
                f.Name = name?.Trim();
                f.Owner = owner?.Trim();
                f.Content = content?.Trim();
                f.CreatedBy = createdBy?.Trim();
                f.UpdatedBy = updatedBy?.Trim();
                f.ImageUlr = imageUrl?.Trim();
                f.Sitecopyright = sitecopyright?.Trim();
                f.LastUpdated = DateTime.UtcNow;

                // Update social media links — remove old, add new
                var existing = f.FooterSocialMedias?.ToList() ?? new List<FooterSocialMedia>();
                _context.FooterSocialMedias.RemoveRange(existing);

                if (socialMediaIds != null && socialMediaIds.Length > 0)
                {
                    foreach (var smId in socialMediaIds)
                    {
                        _context.FooterSocialMedias.Add(new FooterSocialMedia
                        {
                            FooterId = f.Id,
                            SocialId = smId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Footer updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFooterAjax(int id)
        {
            try
            {
                var inUse = await _context.Pages.AnyAsync(p => p.FooterId == id);
                if (inUse) return Json(new { success = false, message = "Cannot delete — this footer is used by one or more pages." });

                var f = await _context.Footers
                    .Include(x => x.FooterSocialMedias)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (f == null) return Json(new { success = false, message = "Footer not found." });

                // Remove social media links first
                if (f.FooterSocialMedias != null && f.FooterSocialMedias.Any())
                    _context.FooterSocialMedias.RemoveRange(f.FooterSocialMedias);

                _context.Footers.Remove(f);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Footer deleted." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }


        // ═══════════════════════════════════════════════════════════
        // SOCIAL MEDIA CRUD (AJAX)
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> GetSocialMedia(int id)
        {
            var s = await _context.SocialMedia.FindAsync(id);
            if (s == null) return Json(new { success = false, message = "Social media not found." });

            return Json(new { success = true, data = new { s.Id, s.Name, s.Url } });
        }

        [HttpPost]
        public async Task<IActionResult> CreateSocialMediaAjax(string name, string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "Name is required." });

                var sm = new SocialMedia
                {
                    Name = name.Trim(),
                    Url = url?.Trim()
                };

                _context.SocialMedia.Add(sm);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Social media link created." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSocialMediaAjax(int id, string name, string url)
        {
            try
            {
                var s = await _context.SocialMedia.FindAsync(id);
                if (s == null) return Json(new { success = false, message = "Social media not found." });

                s.Name = name?.Trim();
                s.Url = url?.Trim();

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Social media updated." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSocialMediaAjax(int id)
        {
            try
            {
                // Check if linked to any footer
                var inUse = await _context.FooterSocialMedias.AnyAsync(fsm => fsm.SocialId == id);
                if (inUse) return Json(new { success = false, message = "Cannot delete — this social media is linked to one or more footers. Remove the link first." });

                var s = await _context.SocialMedia.FindAsync(id);
                if (s == null) return Json(new { success = false, message = "Social media not found." });

                _context.SocialMedia.Remove(s);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Social media deleted." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }


        // ═══════════════════════════════════════════════════════════
        // ADDRESS CRUD (AJAX)
        // ═══════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> GetAddress(int id)
        {
            var a = await _context.Addresss.FindAsync(id);
            if (a == null) return Json(new { success = false, message = "Address not found." });

            return Json(new
            {
                success = true,
                data = new { a.Id, a.Street, a.City, a.State, a.PostalCode, a.Country, a.CVR, a.Email, a.Mobile }
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateAddressAjax(
            string street, string city, string state, string postalCode,
            string country, string cvr, string email, string mobile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(street))
                    return Json(new { success = false, message = "Street is required." });

                var address = new Address
                {
                    Street = street.Trim(),
                    City = city?.Trim(),
                    State = state?.Trim(),
                    PostalCode = postalCode?.Trim(),
                    Country = country?.Trim(),
                    CVR = cvr?.Trim(),
                    Email = email?.Trim(),
                    Mobile = mobile?.Trim()
                };

                _context.Addresss.Add(address);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Address created successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAddressAjax(
            int id, string street, string city, string state, string postalCode,
            string country, string cvr, string email, string mobile)
        {
            try
            {
                var a = await _context.Addresss.FindAsync(id);
                if (a == null) return Json(new { success = false, message = "Address not found." });

                a.Street = street?.Trim();
                a.City = city?.Trim();
                a.State = state?.Trim();
                a.PostalCode = postalCode?.Trim();
                a.Country = country?.Trim();
                a.CVR = cvr?.Trim();
                a.Email = email?.Trim();
                a.Mobile = mobile?.Trim();

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Address updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAddressAjax(int id)
        {
            try
            {
                var a = await _context.Addresss.FindAsync(id);
                if (a == null) return Json(new { success = false, message = "Address not found." });

                _context.Addresss.Remove(a);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Address deleted." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

    }
}
