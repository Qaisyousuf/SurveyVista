using Model;
using System.ComponentModel.DataAnnotations;

namespace Web.ViewModel.QuestionnaireVM
{
    public class EditQuestionnaireViewModel
    {
        public EditQuestionnaireViewModel()
        {
            Questions = new List<Question>();
        }
        public int Id { get; set; }
        [Required]

        [Display(Name ="Questionnaire title")]
        public string? Title { get; set; }
        [Required]
        public string? Description { get; set; }

        public List<Question>? Questions { get; set; }
    
    }
}
