using Microsoft.AspNetCore.Mvc;
using Services.Interaces;

namespace Web.ViewComponents
{
    public class NavigationViewComponent:ViewComponent
    {
        private readonly IPageRepository _pageRepository;

        public NavigationViewComponent(IPageRepository pageRepository)
        {
            _pageRepository = pageRepository;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var pages = await _pageRepository.GetPages();
            return View(pages);
        }
    }
}
