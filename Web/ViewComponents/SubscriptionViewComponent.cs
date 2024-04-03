using Microsoft.AspNetCore.Mvc;
using Model;

namespace Web.ViewComponents
{
    public class SubscriptionViewComponent:ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var subscriber = new Subscription(); // Instantiate a new Subscriber model
            return View(subscriber);
        }
    }
}
