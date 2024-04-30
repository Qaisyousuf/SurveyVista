using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.QuestionnaireVM
{
    public class ResponseQuestionnaireWithUsersViewModel
    {
        public int Id { get; set; }  // Questionnaire ID
        public string? Title { get; set; }  // Title of the questionnaire
        public string? Description { get; set; }  // Description of the questionnaire

        [Required]
        public string? UserName { get; set; }

        [Required]
        public string? Email { get; set; }
        public int ParticipantCount { get; set; }
        public double QuestionsAnsweredPercentage { get; set; }

        public List<ResponseQuestionViewModel> Questions { get; set; } = new List<ResponseQuestionViewModel>();

        public List<ResponseUserViewModel> Users { get; set; }=new List<ResponseUserViewModel> { };

    }
}
