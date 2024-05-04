using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.AccountVM
{
    public class RoleViewModel
    {
        public string? Id { get; set; }  // Role ID, useful for edits

        [Required]
        [Display(Name = "Role Name")]
        public string? Name { get; set; }  // Role name

    }
}
