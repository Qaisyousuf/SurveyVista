using Model;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.FooterVm
{
    public class FooterViewModel
    {
        public int Id { get; set; }
        [Required]
        public string? Title { get; set; }
        [Required]
        public string? Name { get; set; }

        [Required]
        public string? Owner { get; set; }

        [Required]
        public string? Content { get; set; }

        [Required]
        public string? CreatedBy { get; set; }
        [Required]
        public string? UpdatedBy { get; set; }

        public DateTime LastUpdated { get; set; }
        [Required]
        [DataType(DataType.Url)]
        [DisplayName("Image Url")]
        public string? ImageUlr { get; set; }

        [Required]
        public string? Sitecopyright { get; set; }


        public List<SocialMedia>? SocialMediaList { get; set; }
    }
}
