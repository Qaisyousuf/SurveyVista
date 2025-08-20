using Model;
using System.ComponentModel.DataAnnotations;
using Web.ViewModel.AnswerVM;
using Web.ViewModel.QuestionVM;

namespace Web.ViewModel.QuestionnaireVM
{
    public class QuestionnaireViewModel
    {
        public QuestionnaireViewModel()
        {
            Questions = new List<Question>();
            

        }
        public int Id { get; set; }
        [Required]
        [StringLength(40, ErrorMessage = "Title must be between 1 and 40 characters.", MinimumLength = 1)]
        public string? Title { get; set; }
        [Required]
        public string? Description { get; set; }

        public List<Question>? Questions { get; set; }
        public List<Answer>? Answers { get; set; }


        // ADD THESE NEW PROPERTIES (for status management):
        public QuestionnaireStatus Status { get; set; } = QuestionnaireStatus.Draft;
        public DateTime CreatedDate { get; set; }
        public DateTime? PublishedDate { get; set; }
        public DateTime? ArchivedDate { get; set; }

        // Helper properties for the view
        public int ActiveQuestionCount => Questions?.Count(q => q.IsActive) ?? 0;
        public bool HasResponses { get; set; } // We'll set this in the controller


    }
}
