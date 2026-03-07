using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.AccountVM
{
    public class RoleViewModel
    {
        public string? Id { get; set; }

        [Required]
        [Display(Name = "Role Name")]
        public string? Name { get; set; }

        public List<string>? SelectedPermissions { get; set; }
    }
}