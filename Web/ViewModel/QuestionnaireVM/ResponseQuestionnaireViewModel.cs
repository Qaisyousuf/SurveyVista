using System.ComponentModel.DataAnnotations;
using Web.ViewModel.QuestionVM;

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


              // Collection of questions
             public List<ResponseQuestionViewModel> Questions { get; set; } = new List<ResponseQuestionViewModel>();
       

    }
}
