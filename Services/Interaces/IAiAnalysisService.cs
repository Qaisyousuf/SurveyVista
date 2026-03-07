// Services/Interfaces/IAiAnalysisService.cs
using Services.AIViewModel;

namespace Services.Interaces
{
    /// <summary>
    /// Unified AI analysis service powered by Claude API (Anthropic).
    /// Provides sentiment analysis, risk assessment, key phrase extraction,
    /// PII anonymization, workplace insights, and executive reporting.
    /// </summary>
    public interface IAiAnalysisService
    {
        #region Core Analysis Methods

        /// <summary>
        /// Analyzes sentiment of response text (Positive, Negative, Neutral)
        /// with confidence scores using Claude AI.
        /// </summary>
        Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string text);

        /// <summary>
        /// Extracts key phrases, workplace factors, and emotional indicators
        /// from response text using Claude AI.
        /// </summary>
        Task<KeyPhrasesResult> ExtractKeyPhrasesAsync(string text);

        /// <summary>
        /// Removes PII (names, emails, phone numbers, addresses) from text
        /// using Claude AI entity recognition.
        /// </summary>
        Task<string> AnonymizeTextAsync(string text);

        /// <summary>
        /// Detects named entities in text (people, organizations, locations, roles).
        /// </summary>
        Task<List<string>> DetectEntitiesAsync(string text);

        #endregion

        #region Risk Assessment Methods

        /// <summary>
        /// Assesses mental health risk level (Low → Critical) with indicators,
        /// protective factors, and recommended actions using Claude AI.
        /// </summary>
        Task<MentalHealthRiskAssessment> AssessMentalHealthRiskAsync(string anonymizedText, string questionContext);

        /// <summary>
        /// Generates workplace insights and intervention recommendations
        /// categorized by priority and affected areas.
        /// </summary>
        Task<List<WorkplaceInsight>> GenerateWorkplaceInsightsAsync(string anonymizedText, string questionContext);

        /// <summary>
        /// Creates a professional executive summary from aggregated analysis results
        /// suitable for C-level reporting.
        /// </summary>
        Task<string> GenerateExecutiveSummaryAsync(List<ResponseAnalysisResult> analysisResults);

        /// <summary>
        /// Categorizes response into workplace mental health themes
        /// (Work-Life Balance, Burnout, Leadership, etc.).
        /// </summary>
        Task<List<string>> CategorizeResponseAsync(string anonymizedText);

        #endregion

        #region Composite Analysis Methods

        /// <summary>
        /// Performs full analysis pipeline on a single response:
        /// Anonymize → Sentiment → Key Phrases → Risk → Insights.
        /// </summary>
        Task<ResponseAnalysisResult> AnalyzeCompleteResponseAsync(AnalysisRequest request);

        /// <summary>
        /// Analyzes multiple responses for a specific question.
        /// </summary>
        Task<List<ResponseAnalysisResult>> AnalyzeQuestionResponsesAsync(int questionId, List<AnalysisRequest> requests);

        /// <summary>
        /// Generates comprehensive analysis overview for an entire questionnaire
        /// including sentiment distribution, risk breakdown, and executive summary.
        /// </summary>
        Task<QuestionnaireAnalysisOverview> GenerateQuestionnaireOverviewAsync(int questionnaireId);

        /// <summary>
        /// Batch processes multiple responses with rate-limit-aware concurrency.
        /// </summary>
        Task<List<ResponseAnalysisResult>> BatchAnalyzeResponsesAsync(List<AnalysisRequest> requests);

        #endregion

        #region Mental Health Intelligence

        /// <summary>
        /// Identifies responses flagged as High or Critical risk
        /// requiring immediate organizational attention.
        /// </summary>
        Task<List<ResponseAnalysisResult>> IdentifyHighRiskResponsesAsync(int questionnaireId);

        /// <summary>
        /// Analyzes mental health trends across a date range.
        /// </summary>
        Task<List<WorkplaceInsight>> AnalyzeMentalHealthTrendsAsync(int questionnaireId, DateTime fromDate, DateTime toDate);

        /// <summary>
        /// Compares mental health metrics across team identifiers.
        /// </summary>
        Task<Dictionary<string, QuestionnaireAnalysisOverview>> CompareTeamMentalHealthAsync(int questionnaireId, List<string> teamIdentifiers);

        /// <summary>
        /// Generates prioritized intervention recommendations based on analysis.
        /// </summary>
        Task<List<WorkplaceInsight>> GenerateInterventionRecommendationsAsync(int questionnaireId);

        #endregion

        #region Reporting

        /// <summary>
        /// Creates a detailed markdown analysis report for management review.
        /// </summary>
        Task<string> GenerateDetailedAnalysisReportAsync(int questionnaireId);

        /// <summary>
        /// Exports fully anonymized analysis data for external processing.
        /// </summary>
        Task<List<ResponseAnalysisResult>> ExportAnonymizedAnalysisAsync(int questionnaireId);

        /// <summary>
        /// Generates management dashboard data with KPIs and summaries.
        /// </summary>
        Task<QuestionnaireAnalysisOverview> GenerateManagementDashboardAsync(int questionnaireId);

        #endregion

        #region Service Health

        /// <summary>
        /// Tests the Claude API connection with a minimal request.
        /// </summary>
        Task<bool> TestClaudeConnectionAsync();

        /// <summary>
        /// Validates an analysis request before processing.
        /// </summary>
        Task<bool> ValidateAnalysisRequestAsync(AnalysisRequest request);

        /// <summary>
        /// Returns service health status. Key: "Claude", Value: online/offline.
        /// </summary>
        Task<Dictionary<string, bool>> GetServiceHealthStatusAsync();

        #endregion
    }
}