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
        public string? Title { get; set; }
        [Required]
        public string? Description { get; set; }

        public List<Question>? Questions { get; set; }
        public List<Answer>? Answers { get; set; }





    }
}
