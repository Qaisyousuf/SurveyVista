using System.ComponentModel.DataAnnotations;
namespace Web.ViewModel.NewsLetterVM
{
    public class PdfUploadViewModel
    {
        [Required(ErrorMessage = "Please upload a file.")]
        public IFormFile SubscriberFile { get; set; }
    }
}
