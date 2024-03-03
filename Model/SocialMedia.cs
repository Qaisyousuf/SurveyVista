using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class SocialMedia
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }
        [Required]

        [DataType(DataType.Url)]
        public string? Url { get; set; }

        public List<FooterSocialMedia>? FooterSocialMedias { get; set; }


       
    }
}
