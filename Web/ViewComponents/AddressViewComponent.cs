using Microsoft.AspNetCore.Mvc;
using Services.Implemnetation;
using Services.Interaces;

namespace Web.ViewComponents
{
    public class AddressViewComponent:ViewComponent
    {
        private readonly IAddressRepository _addressRepository;

        public AddressViewComponent(IAddressRepository addressRepository)
        {
            _addressRepository = addressRepository;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            
            var address = await _addressRepository.GetAddressesAsync();

            return View(address);
        }
    }
}
