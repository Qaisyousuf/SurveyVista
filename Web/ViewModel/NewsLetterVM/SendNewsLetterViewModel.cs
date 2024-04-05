using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata.Ecma335;

namespace Web.ViewModel.NewsLetterVM
{
    public class SendNewsLetterViewModel
    {
        [Required]
        public string? Subject { get; set; }
        [Required]
        public string? Body { get; set; }
    }
}
