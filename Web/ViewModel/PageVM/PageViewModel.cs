using Model;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.ViewModel.PageVM
{
    public class PageViewModel
    {
        public int Id { get; set; }
        [Required]
        public string? Title { get; set; }
        public string? Slug { get; set; }
        [Required]
        public string? Content { get; set; }


        [DisplayName("Footer")]
        public int FooterId { get; set; }

        [ForeignKey("FooterId")]
        public Footer? Footer { get; set; }


        [DisplayName("Banner")]
        public int BannerId { get; set; }

        [ForeignKey("BannerId")]
        public Banner? banner { get; set; }
    }
}
