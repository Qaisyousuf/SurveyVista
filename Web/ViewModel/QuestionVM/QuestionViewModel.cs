using Model;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Web.ViewModel.AnswerVM;


namespace Web.ViewModel.QuestionVM
{
    public class QuestionViewModel
    {
        public int Id { get; set; }
        [Required]
        public string? Text { get; set; }
        public QuestionType Type { get; set; }

        // Foreign key for Questionnaire

        public int QuestionnaireId { get; set; } // Foreign key for Questionnaire
        [ForeignKey("QuestionnaireId")]
        public Questionnaire? Questionnaire { get; set; }

        public List<AnswerViewModel>? AnswersViewModel { get; set; }=new List<AnswerViewModel>();
    }
}
