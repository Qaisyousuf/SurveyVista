using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Banner
    {
        public int Id { get; set; }

        [Required]

        
        public string? Title { get; set; }
        [Required]
        public string? Description { get; set; }
        [Required]
        public string? Content { get; set; }
        [Required]
        public string? LinkUrl { get; set; }
        [Required]
        public string? ImageUrl { get; set; }



    }
}
