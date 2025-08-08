using Model;

namespace Web.ViewModel.QuestionnaireVM
{
    public class SetLogicViewModel
    {
        public int QuestionnaireId { get; set; }
        public string QuestionnaireName { get; set; } = string.Empty;
        public List<QuestionLogicViewModel> Questions { get; set; } = new List<QuestionLogicViewModel>();
    }

    // ViewModel for each question in the logic setup
    public class QuestionLogicViewModel
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; }
        public int QuestionNumber { get; set; }
        public List<AnswerConditionViewModel> Answers { get; set; } = new List<AnswerConditionViewModel>();
    }

    // ViewModel for each answer's condition
    public class AnswerConditionViewModel
    {
        public int AnswerId { get; set; }
        public string AnswerText { get; set; } = string.Empty;
        public ConditionActionType ActionType { get; set; } = ConditionActionType.Continue;
        public int? TargetQuestionNumber { get; set; }
        public int? SkipCount { get; set; }
        public string? EndMessage { get; set; }
    }

    // Enum for condition action types
    public enum ConditionActionType
    {
        Continue = 0,
        SkipToQuestion = 1,
        SkipCount = 2,
        EndSurvey = 3
    }

    // ViewModel for saving conditions
    public class SaveConditionsViewModel
    {
        public int QuestionnaireId { get; set; }
        public List<AnswerConditionSaveViewModel> Conditions { get; set; } = new List<AnswerConditionSaveViewModel>();
    }

    public class AnswerConditionSaveViewModel
    {
        public int AnswerId { get; set; }
        public string ConditionJson { get; set; } = string.Empty;
    }
}
