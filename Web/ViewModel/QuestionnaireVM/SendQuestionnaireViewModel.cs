using NuGet.Protocol.Core.Types;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.QuestionnaireVM
{
    public class SendQuestionnaireViewModel
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }
        [Required]
        public string? Email { get; set; }

        [Required]
        [DisplayName("Set expiration date and time for the URL")]
        public DateTime? ExpirationDateTime { get; set; }

        public int QuestionnaireId { get; set; }

    }
}
