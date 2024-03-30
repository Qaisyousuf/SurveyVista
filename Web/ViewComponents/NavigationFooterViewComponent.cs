using Microsoft.AspNetCore.Mvc;
using Services.Interaces;

namespace Web.ViewComponents
{
    public class NavigationFooterViewComponent:ViewComponent
    {
        private readonly IPageRepository _pageRepository;

        public NavigationFooterViewComponent(IPageRepository pageRepository)
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
