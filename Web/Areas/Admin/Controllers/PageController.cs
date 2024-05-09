using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Model;
using Services.Interaces;
using Services.SlugServices;
using Web.ViewModel.PageVM;

namespace Web.Areas.Admin.Controllers
{

    
    public class PageController : Controller
    {
        private readonly IPageRepository _pageRepository;
        private readonly IBannerRepository _bannerRepository;
        private readonly IFooterRepository _footerRepository;

        public PageController(IPageRepository pageRepository,IBannerRepository bannerRepository, IFooterRepository footerRepository)
        {
         _pageRepository = pageRepository;
         _bannerRepository = bannerRepository;
          _footerRepository = footerRepository;
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


    }
}
