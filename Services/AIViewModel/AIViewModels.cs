// Services/AIViewModel/AIAnalysisViewModels.cs
namespace Services.AIViewModel
{
    /// <summary>
    /// Risk severity levels for mental health assessment.
    /// </summary>
    public enum RiskLevel
    {
        Low = 1,
        Moderate = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Sentiment analysis output with confidence breakdown.
    /// </summary>
    public class SentimentAnalysisResult
    {
        public string Sentiment { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public double PositiveScore { get; set; }
        public double NegativeScore { get; set; }
        public double NeutralScore { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Key phrases extracted with workplace and emotional categorization.
    /// </summary>
    public class KeyPhrasesResult
    {
        public List<string> KeyPhrases { get; set; } = new();
        public List<string> WorkplaceFactors { get; set; } = new();
        public List<string> EmotionalIndicators { get; set; } = new();
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Mental health risk assessment with indicators and recommendations.
    /// </summary>
    public class MentalHealthRiskAssessment
    {
        public RiskLevel RiskLevel { get; set; }
        public double RiskScore { get; set; }
        public List<string> RiskIndicators { get; set; } = new();
        public List<string> ProtectiveFactors { get; set; } = new();
        public bool RequiresImmediateAttention { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Workplace insight with categorized issue and intervention recommendation.
    /// </summary>
    public class WorkplaceInsight
    {
        public string Category { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public string RecommendedIntervention { get; set; } = string.Empty;
        public int Priority { get; set; }
        public List<string> AffectedAreas { get; set; } = new();
        public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Complete analysis result for a single response — aggregates all AI outputs.
    /// </summary>
    public class ResponseAnalysisResult
    {
        public int ResponseId { get; set; }
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string ResponseText { get; set; } = string.Empty;
        public string AnonymizedResponseText { get; set; } = string.Empty;

        public SentimentAnalysisResult? SentimentAnalysis { get; set; }
        public KeyPhrasesResult? KeyPhrases { get; set; }
        public MentalHealthRiskAssessment? RiskAssessment { get; set; }
        public List<WorkplaceInsight> Insights { get; set; } = new();

        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public bool IsAnalysisComplete { get; set; }
    }

    /// <summary>
    /// Aggregated analysis overview for an entire questionnaire.
    /// </summary>
    public class QuestionnaireAnalysisOverview
    {
        public int QuestionnaireId { get; set; }
        public string QuestionnaireTitle { get; set; } = string.Empty;
        public int TotalResponses { get; set; }
        public int AnalyzedResponses { get; set; }

        // Sentiment distribution (0.0–1.0)
        public double OverallPositiveSentiment { get; set; }
        public double OverallNegativeSentiment { get; set; }
        public double OverallNeutralSentiment { get; set; }

        // Risk distribution counts
        public int LowRiskResponses { get; set; }
        public int ModerateRiskResponses { get; set; }
        public int HighRiskResponses { get; set; }
        public int CriticalRiskResponses { get; set; }

        // Insights
        public List<WorkplaceInsight> TopWorkplaceIssues { get; set; } = new();
        public List<string> MostCommonKeyPhrases { get; set; } = new();

        public DateTime LastAnalyzedAt { get; set; }
        public string ExecutiveSummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Input request for analysis pipeline.
    /// </summary>
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

    public class UserTrajectoryAnalysis
    {
        // ── Overall Trajectory ──
        /// <summary>
        /// "Improving", "Stable", "Declining", "Fluctuating", "Initial" (single response)
        /// </summary>
        public string TrajectoryDirection { get; set; } = "Initial";

        /// <summary>
        /// Overall wellness score 0-100
        /// </summary>
        public int TrajectoryScore { get; set; }

        /// <summary>
        /// Change from first to latest response (e.g., +15 or -20). 0 if single response.
        /// </summary>
        public int ScoreChange { get; set; }

        /// <summary>
        /// "Low", "Moderate", "High", "Critical"
        /// </summary>
        public string OverallRiskLevel { get; set; } = "Low";

        /// <summary>
        /// 2-3 sentence overview
        /// </summary>
        public string ExecutiveSummary { get; set; } = string.Empty;

        /// <summary>
        /// Longer detailed analysis paragraph
        /// </summary>
        public string DetailedAnalysis { get; set; } = string.Empty;

        // ── Per-Response Snapshots ──
        public List<ResponseSnapshot> ResponseSnapshots { get; set; } = new();

        // ── Cross-Response Patterns ──
        public List<PatternInsight> PatternInsights { get; set; } = new();

        // ── Strengths & Concerns ──
        public List<StrengthFactor> StrengthFactors { get; set; } = new();
        public List<ConcernFactor> ConcernFactors { get; set; } = new();

        // ── Recommendations ──
        public List<TrajectoryRecommendation> Recommendations { get; set; } = new();

        // ── Narrative ──
        /// <summary>
        /// A story-like professional summary suitable for reports
        /// </summary>
        public string TimelineNarrative { get; set; } = string.Empty;

        // ── Metadata ──
        public int TotalResponsesAnalyzed { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public bool IsIncremental { get; set; } = false;
    }

    public class ResponseSnapshot
    {
        /// <summary>
        /// The database Response.Id
        /// </summary>
        public int ResponseId { get; set; }

        public string ResponseDate { get; set; } = string.Empty;

        public string QuestionnaireName { get; set; } = string.Empty;

        /// <summary>
        /// Wellness score 0-100 for this specific response
        /// </summary>
        public int WellnessScore { get; set; }

        /// <summary>
        /// "Low", "Moderate", "High", "Critical"
        /// </summary>
        public string RiskLevel { get; set; } = "Low";

        /// <summary>
        /// "Positive", "Negative", "Mixed", "Neutral"
        /// </summary>
        public string SentimentLabel { get; set; } = "Neutral";

        /// <summary>
        /// Key themes detected (e.g., "workload", "management", "isolation")
        /// </summary>
        public List<string> KeyThemes { get; set; } = new();

        /// <summary>
        /// One-sentence summary of this response
        /// </summary>
        public string BriefSummary { get; set; } = string.Empty;
    }

    public class PatternInsight
    {
        /// <summary>
        /// Description of the pattern (e.g., "Recurring workload concerns across all responses")
        /// </summary>
        public string Pattern { get; set; } = string.Empty;

        /// <summary>
        /// "High", "Medium", "Low"
        /// </summary>
        public string Severity { get; set; } = "Medium";

        /// <summary>
        /// When this pattern was first observed
        /// </summary>
        public string FirstSeen { get; set; } = string.Empty;

        /// <summary>
        /// Whether this pattern is still present in the latest response
        /// </summary>
        public bool StillPresent { get; set; } = true;
    }

    public class StrengthFactor
    {
        public string Factor { get; set; } = string.Empty;
    }

    public class ConcernFactor
    {
        public string Concern { get; set; } = string.Empty;

        /// <summary>
        /// "Immediate", "Monitor", "Low"
        /// </summary>
        public string Urgency { get; set; } = "Monitor";
    }

    public class TrajectoryRecommendation
    {
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// "Urgent", "High", "Normal"
        /// </summary>
        public string Priority { get; set; } = "Normal";

        /// <summary>
        /// "Workplace", "Personal", "Professional Support"
        /// </summary>
        public string Category { get; set; } = "Workplace";
    }
}