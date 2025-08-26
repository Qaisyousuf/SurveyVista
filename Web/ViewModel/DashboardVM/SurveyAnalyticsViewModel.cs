// UPDATE your SurveyAnalyticsViewModel.cs to include these new properties:

namespace Web.ViewModel.DashboardVM
{
    public class SurveyAnalyticsViewModel
    {
        // Existing properties
        public int TotalQuestionnaires { get; set; }
        public int TotalResponses { get; set; }
        public int TotalParticipants { get; set; }
        public double AvgQuestionsPerSurvey { get; set; }
        public double CompletionRate { get; set; }
        public int MonthlyActiveUsers { get; set; }
        public double AvgResponseTime { get; set; }

        // 🔥 NEW REAL DATA PROPERTIES
        public double QualityScore { get; set; }
        public double ResponseRateTrend { get; set; }
        public TrendDataViewModel TrendData { get; set; } = new TrendDataViewModel();
        public List<WeeklyActivityViewModel> WeeklyActivityData { get; set; } = new List<WeeklyActivityViewModel>();

        // Existing list properties
        public List<QuestionTypeStatViewModel> QuestionTypeDistribution { get; set; } = new List<QuestionTypeStatViewModel>();
        public List<DailyResponseViewModel> RecentResponses { get; set; } = new List<DailyResponseViewModel>();
        public List<SurveyPerformanceViewModel> TopSurveys { get; set; } = new List<SurveyPerformanceViewModel>();
        public List<RecentActivityViewModel> RecentActivity { get; set; } = new List<RecentActivityViewModel>();
    }

    // 🔥 NEW: Trend Data ViewModel
    public class TrendDataViewModel
    {
        public double ResponsesTrend { get; set; }
        public double QuestionnairesTrend { get; set; }
        public double CompletionTrend { get; set; }
        public double ResponseTimeTrend { get; set; }

        public string ResponsesTrendText => ResponsesTrend >= 0 ? $"+{ResponsesTrend}% from last month" : $"{ResponsesTrend}% from last month";
        public string QuestionnairesTrendText => QuestionnairesTrend >= 0 ? $"+{QuestionnairesTrend}% from last month" : $"{QuestionnairesTrend}% from last month";
        public string CompletionTrendText => CompletionTrend >= 0 ? $"+{CompletionTrend}% from last month" : $"{CompletionTrend}% from last month";
        public string ResponseTimeTrendText => ResponseTimeTrend <= 0 ? $"{Math.Abs(ResponseTimeTrend)} minutes faster" : $"+{ResponseTimeTrend} minutes slower";

        public bool ResponsesTrendPositive => ResponsesTrend >= 0;
        public bool QuestionnairesTrendPositive => QuestionnairesTrend >= 0;
        public bool CompletionTrendPositive => CompletionTrend >= 0;
        public bool ResponseTimeTrendPositive => ResponseTimeTrend <= 0; // Lower time is better
    }

    // 🔥 NEW: Weekly Activity ViewModel
    public class WeeklyActivityViewModel
    {
        public string Day { get; set; } = string.Empty;
        public double ResponseRate { get; set; }
        public int ActiveUsers { get; set; }
        public int Responses { get; set; }
    }

    // Existing ViewModels remain the same
    public class QuestionTypeStatViewModel
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DailyResponseViewModel
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class SurveyPerformanceViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int ResponseCount { get; set; }
        public double CompletionRate { get; set; }
    }

    public class RecentActivityViewModel
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Icon { get; set; } = string.Empty;

        public int ResponseId { get; set; }
        public int QuestionnaireId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
    }
}