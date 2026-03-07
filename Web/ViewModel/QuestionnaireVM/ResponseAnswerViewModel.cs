namespace Web.ViewModel.QuestionnaireVM
{
    public class ResponseAnswerViewModel
    {
        public int Id { get; set; }
        public string? Text { get; set; }
        public int? Count { get; set; }

        // NEW: Must be mapped from Answer entity
        public bool IsOtherOption { get; set; } = false;
        public string? ConditionJson { get; set; }
    }
}
