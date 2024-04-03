using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Subscription
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        public bool IsSubscribed { get; set; }
    }
}
