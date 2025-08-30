// Services/AIViewModel/AIAnalysisViewModels.cs
namespace Services.AIViewModel
{
    public enum RiskLevel
    {
        Low = 1,
        Moderate = 2,
        High = 3,
        Critical = 4
    }

    public class SentimentAnalysisResult
    {
        public string Sentiment { get; set; } = string.Empty; // Positive, Negative, Neutral
        public double ConfidenceScore { get; set; }
        public double PositiveScore { get; set; }
        public double NegativeScore { get; set; }
        public double NeutralScore { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    public class KeyPhrasesResult
    {
        public List<string> KeyPhrases { get; set; } = new List<string>();
        public List<string> WorkplaceFactors { get; set; } = new List<string>();
        public List<string> EmotionalIndicators { get; set; } = new List<string>();
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    }

    public class MentalHealthRiskAssessment
    {
        public RiskLevel RiskLevel { get; set; }
        public double RiskScore { get; set; } // 0-1 scale
        public List<string> RiskIndicators { get; set; } = new List<string>();
        public List<string> ProtectiveFactors { get; set; } = new List<string>();
        public bool RequiresImmediateAttention { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    }

    public class WorkplaceInsight
    {
        public string Category { get; set; } = string.Empty; // e.g., "Work-Life Balance", "Management", "Workload"
        public string Issue { get; set; } = string.Empty;
        public string RecommendedIntervention { get; set; } = string.Empty;
        public int Priority { get; set; } // 1-5 scale
        public List<string> AffectedAreas { get; set; } = new List<string>();
        public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
    }

    public class ResponseAnalysisResult
    {
        public int ResponseId { get; set; }
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string ResponseText { get; set; } = string.Empty;
        public string AnonymizedResponseText { get; set; } = string.Empty; // PII removed

        // Azure Language Service Results
        public SentimentAnalysisResult? SentimentAnalysis { get; set; }
        public KeyPhrasesResult? KeyPhrases { get; set; }

        // Azure OpenAI Results
        public MentalHealthRiskAssessment? RiskAssessment { get; set; }
        public List<WorkplaceInsight> Insights { get; set; } = new List<WorkplaceInsight>();

        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public bool IsAnalysisComplete { get; set; } = false;
    }

    public class QuestionnaireAnalysisOverview
    {
        public int QuestionnaireId { get; set; }
        public string QuestionnaireTitle { get; set; } = string.Empty;
        public int TotalResponses { get; set; }
        public int AnalyzedResponses { get; set; }

        // Overall Statistics
        public double OverallPositiveSentiment { get; set; }
        public double OverallNegativeSentiment { get; set; }
        public double OverallNeutralSentiment { get; set; }

        // Risk Distribution
        public int LowRiskResponses { get; set; }
        public int ModerateRiskResponses { get; set; }
        public int HighRiskResponses { get; set; }
        public int CriticalRiskResponses { get; set; }

        // Top Issues
        public List<WorkplaceInsight> TopWorkplaceIssues { get; set; } = new List<WorkplaceInsight>();
        public List<string> MostCommonKeyPhrases { get; set; } = new List<string>();

        public DateTime LastAnalyzedAt { get; set; }
        public string ExecutiveSummary { get; set; } = string.Empty;
    }

    public class AnalysisRequest
    {
        public int ResponseId { get; set; }
        public int QuestionId { get; set; }
        public string ResponseText { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string QuestionType { get; set; } = string.Empty;
        public bool IncludeSentimentAnalysis { get; set; } = true;
        public bool IncludeKeyPhraseExtraction { get; set; } = true;
        public bool IncludeRiskAssessment { get; set; } = true;
        public bool IncludeWorkplaceInsights { get; set; } = true;
    }
}