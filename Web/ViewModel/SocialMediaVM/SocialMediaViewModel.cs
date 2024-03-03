using Model;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.SocialMediaVM
{
    public class SocialMediaViewModel
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }
        [Required]

        [DataType(DataType.Url)]
        public string? Url { get; set; }



    }
}
