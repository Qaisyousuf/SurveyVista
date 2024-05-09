using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.NewsLetterVM
{
    public class EmailEventViewModel
    {
        [Required]
        public string Email { get; set; }

        [Required]
        public string EventType { get; set; }

        [Required]
        public DateTime EventTime { get; set; }
    }
}
