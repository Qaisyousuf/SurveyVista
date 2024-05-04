using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.AccountVM
{
    public class EditUserViewModel
    {
        public string? Id { get; set; }

        [Required(ErrorMessage = "First Name is required")]
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Required(ErrorMessage = "Last Name is required")]
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Display(Name = "Roles")]
        public List<string>? SelectedRoles { get; set; }

        public List<SelectListItem>? Roles { get; set; }
    }
}
