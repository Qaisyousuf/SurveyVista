using Model;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.ViewModel.AnswerVM
{
    public class AnswerViewModel
    {
        public int Id { get; set; }
        public string? Text { get; set; }
        public int QuestionId { get; set; } // Foreign key for Question
        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }
    }
}
