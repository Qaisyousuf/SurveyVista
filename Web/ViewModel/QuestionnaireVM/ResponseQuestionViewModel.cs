using Model;
using Web.ViewModel.AnswerVM;

namespace Web.ViewModel.QuestionnaireVM
{
    public class ResponseQuestionViewModel
    {
        public int Id { get; set; }  // Question ID
        public string? Text { get; set; }  // Question text
        public QuestionType Type { get; set; }  // Question type

        // List of selectable answers
        public List<ResponseAnswerViewModel> Answers { get; set; } = new List<ResponseAnswerViewModel>();

        // IDs of selected answers, used for submitting form data
        public List<int> SelectedAnswerIds { get; set; } = new List<int>();

        public List<string> SelectedText { get; set; } = new List<string>();
    }
}
