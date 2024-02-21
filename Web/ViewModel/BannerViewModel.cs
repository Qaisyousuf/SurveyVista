using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel
{
    public class BannerViewModel
    {
        public int Id { get; set; }

        [Required]
        public string? Title { get; set; }
        [Required]
        public string? Description { get; set; }
        [Required]
        public string? Content { get; set; }
        [Required]
        [DisplayName("Link Url")]
        public string? LinkUrl { get; set; }
        [Required]

        [DisplayName("Image Url")]
        public string? ImageUrl { get; set; }
    }
}
