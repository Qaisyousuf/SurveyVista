using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class FooterSocialMedia
    {

        public int FooterId { get; set; }


        [ForeignKey("FooterId")]
        public Footer? Footer { get; set; }


        public int SocialId { get; set; }

        [ForeignKey("SocialId")]

        public SocialMedia? SocialMedia { get; set; }


    }
}
