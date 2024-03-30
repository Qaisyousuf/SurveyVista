using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Page
    {
        public int Id { get; set; }
        [Required]
        public string? Title { get; set; }
        public string? Slug { get; set; }
        [Required]
        public string? Content { get; set; }


        public int FooterId { get; set; }

        [ForeignKey("FooterId")]
        public Footer? footer { get; set; }

        public int BannerId { get; set; }

        [ForeignKey("BannerId")]
        public Banner? banner { get; set; }
    }
}
