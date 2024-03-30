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





    }
}
