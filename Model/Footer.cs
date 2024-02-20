using System.ComponentModel.DataAnnotations;

namespace Model
{
    public class Footer
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
        public string? UpdatedBy { get; set;}

        public DateTime LastUpdated { get; set; }
        [Required]
        public string?  ImageUlr { get; set; }

        [Required]
        public string? Sitecopyright { get; set;}


    }
}
