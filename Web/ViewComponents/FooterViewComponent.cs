using Microsoft.AspNetCore.Mvc;
using Services.Implemnetation;
using Services.Interaces;

namespace Web.ViewComponents
{
    public class FooterViewComponent:ViewComponent
    {
        private readonly IFooterRepository _footerRepository;

        public FooterViewComponent(IFooterRepository footerRepository)
        {
            _footerRepository = footerRepository;
        }


        public IViewComponentResult Invoke()
        {
            if (!TempData.ContainsKey("Footer"))
            {
                // Handle the case where "Footer" is not found in TempData
                // For example, return a default view or perform a different action
                return View("DefaultFooterView"); // Return a default view named "DefaultFooterView"
            }

            if (!int.TryParse(TempData["Footer"].ToString(), out int footerId))
            {
                // Handle the case where "Footer" value is not a valid integer
                // For example, return a default view or perform a different action
                return View("DefaultFooterView"); // Return a default view named "DefaultFooterView"
            }

            var footer = _footerRepository.GetFooterByIdWithSocialMedia(footerId);

            if (footer == null)
            {
                // Handle the case where the footer is not found
                // For example, return a default view or perform a different action
                return View("DefaultFooterView"); // Return a default view named "DefaultFooterView"
            }

            return View(footer);
        }

        //public IViewComponentResult Invoke()
        //{
        //    int footerId = (int)TempData["bannerId"];
        //    var footer = _footerRepository.GetFooterById(footerId);
        //    return View(footer);
        //}
    }
}
