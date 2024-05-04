using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model;
using Services.Interaces;
using Web.ViewModel.AddressVM;

namespace Web.Areas.Admin.Controllers
{
    [Authorize(Roles ="Admin")]    
    public class AddressController : Controller
    {
        private readonly IAddressRepository _addresContext;

        public AddressController(IAddressRepository addresContext)
        {
            _addresContext = addresContext;
        }
        public IActionResult Index()
        {

            var addressFromdb = _addresContext.GetAddresses();

            var viewmodel=new List<AddressViewModel>();


            foreach (var address in addressFromdb)
            {
                viewmodel.Add(new AddressViewModel
                {
                    Id=address.Id,
                    State=address.State,
                    Street=address.Street,
                    City=address.City,
                    PostalCode=address.PostalCode,
                    Country=address.Country,
                    CVR=address.CVR,
                    Email=address.Email,
                    Mobile=address.Mobile,
                    
                });
            }

            return View(viewmodel);
        }

        [HttpGet]

        public IActionResult Create()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Create(AddressViewModel viewmodel) 
        {

            if(ModelState.IsValid)
            {

                var addres = new Address
                {
                    Id=viewmodel.Id,
                    State=viewmodel.State,
                    Street=viewmodel.Street,
                    City=viewmodel.City,
                    PostalCode=viewmodel.PostalCode,
                    Country=viewmodel.Country,
                    CVR=viewmodel.CVR,
                    Email=viewmodel.Email,
                    Mobile=viewmodel.Mobile,

                };


               await _addresContext.Add(addres);

                await _addresContext.commitAsync();
                TempData["Success"] = "Address created successfully";

                return RedirectToAction(nameof(Index));
            }

            return View(viewmodel);

        }


        [HttpGet]
        public IActionResult Edit(int id)
        {

            var address=_addresContext.GetAddressById(id);

            var viewmodel = new AddressViewModel
            {
                Id=address.Id,
                State=address.State,
                Street=address.Street,
                City=address.City,
                PostalCode=address.PostalCode,
                Country=address.Country,
                CVR=address.CVR,
                Email=address.Email,
                Mobile=address.Mobile,

            };

            return View(viewmodel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(AddressViewModel viewmodel)
        {
            if(ModelState.IsValid)
            {

                var addres = _addresContext.GetAddressById(viewmodel.Id);

                addres.Id = viewmodel.Id;
                addres.State = viewmodel.State;
                addres.Street = viewmodel.Street;
                addres.City = viewmodel.City;
                addres.Country = viewmodel.Country;
                addres.Email = viewmodel.Email;
                addres.Mobile = viewmodel.Mobile;
                addres.CVR = viewmodel.CVR;
                addres.PostalCode = viewmodel.PostalCode;

                _addresContext.Update(addres);

                await _addresContext.commitAsync();
                TempData["Success"] = "Address updated successfully";

                return RedirectToAction(nameof(Index));

            }
            return View(viewmodel);
        }



        [HttpGet]
        public IActionResult Delete(int id)
        {
            var address = _addresContext.GetAddressById(id);

            var viewmodel = new AddressViewModel
            {
                Id = address.Id,
                State = address.State,
                Street = address.Street,
                City = address.City,
                PostalCode = address.PostalCode,
                Country = address.Country,
                CVR = address.CVR,
                Email = address.Email,
                Mobile = address.Mobile,

            };

            return View(viewmodel);
        }

        [HttpPost]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirm(int id)
        {
             _addresContext.Delete(id);

            await _addresContext.commitAsync();
            TempData["Success"] = "Address deleted successfully";

            return RedirectToAction(nameof(Index));
        }
    }
}
