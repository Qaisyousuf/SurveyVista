using System.ComponentModel.DataAnnotations;


namespace Web.ViewModel.QuestionnaireVM
{
    public class ResponseQuestionnaireViewModel
    {
       
            public int Id { get; set; }  // Questionnaire ID
            public string? Title { get; set; }  // Title of the questionnaire
            public string? Description { get; set; }  // Description of the questionnaire

            [Required]
            public string? UserName { get; set; }

             [Required]
             public string? Email { get; set; }
            public int ParticipantCount { get; set; }
             public int QuestionsAnsweredCount { get; set; }

      
            public List<ResponseQuestionViewModel> Questions { get; set; } = new List<ResponseQuestionViewModel>();
       

    }
}
