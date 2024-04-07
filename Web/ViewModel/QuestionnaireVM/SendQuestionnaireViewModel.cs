using NuGet.Protocol.Core.Types;
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

        public int QuestionnaireId { get; set; }

    }
}
