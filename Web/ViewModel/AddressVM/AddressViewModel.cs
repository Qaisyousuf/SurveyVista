using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.AddressVM
{
    public class AddressViewModel
    {
        public int Id { get; set; }

        [Required]
        public string? Street { get; set; }

        [Required]
        public string? City { get; set; }

        public string? State { get; set; }
        [Required]


        [DataType(DataType.PostalCode)]
        public string? PostalCode { get; set; }
        [Required]
        public string? Country { get; set; }

        public string? CVR { get; set; }

        [Required]
        [DataType(DataType.EmailAddress)]
        public string? Email { get; set; }
        [Required]

        [DataType(DataType.PhoneNumber)]
        public string? Mobile { get; set; }
    }
}
