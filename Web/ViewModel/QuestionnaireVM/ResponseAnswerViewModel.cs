namespace Web.ViewModel.QuestionnaireVM
{
    public class ResponseAnswerViewModel
    {
        public int Id { get; set; }  // Answer ID
        public string? Text { get; set; }  // Answer text

        public int? Count { get; set; }
        public bool IsOtherOption { get; set; } = false;
        public string? ConditionJson { get; set; }  // Add this line for conditional logic
    }
}
