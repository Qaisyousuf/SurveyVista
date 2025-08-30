// Services/Interfaces/IAiAnalysisService.cs
using Services.AIViewModel;

namespace Services.Interaces
{
    public interface IAiAnalysisService
    {
        #region Azure Language Service Methods

        /// <summary>
        /// Analyzes sentiment of response text using Azure Language Service
        /// </summary>
        Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string text);

        /// <summary>
        /// Extracts key phrases and workplace factors from response text
        /// </summary>
        Task<KeyPhrasesResult> ExtractKeyPhrasesAsync(string text);

        /// <summary>
        /// Removes PII (Personally Identifiable Information) from response text
        /// </summary>
        Task<string> AnonymizeTextAsync(string text);

        /// <summary>
        /// Detects entities in text (workplace factors, departments, roles, etc.)
        /// </summary>
        Task<List<string>> DetectEntitiesAsync(string text);

        #endregion

        #region Azure OpenAI Methods

        /// <summary>
        /// Assesses mental health risk level using GPT-3.5 Turbo
        /// </summary>
        Task<MentalHealthRiskAssessment> AssessMentalHealthRiskAsync(string anonymizedText, string questionContext);

        /// <summary>
        /// Generates workplace insights and intervention recommendations
        /// </summary>
        Task<List<WorkplaceInsight>> GenerateWorkplaceInsightsAsync(string anonymizedText, string questionContext);

        /// <summary>
        /// Creates executive summary for questionnaire analysis
        /// </summary>
        Task<string> GenerateExecutiveSummaryAsync(List<ResponseAnalysisResult> analysisResults);

        /// <summary>
        /// Categorizes responses into mental health themes
        /// </summary>
        Task<List<string>> CategorizeResponseAsync(string anonymizedText);

        #endregion

        #region Combined Analysis Methods

        /// <summary>
        /// Performs complete AI analysis on a single response (both Azure services)
        /// </summary>
        Task<ResponseAnalysisResult> AnalyzeCompleteResponseAsync(AnalysisRequest request);

        /// <summary>
        /// Analyzes multiple responses for a specific question
        /// </summary>
        Task<List<ResponseAnalysisResult>> AnalyzeQuestionResponsesAsync(int questionId, List<AnalysisRequest> requests);

        /// <summary>
        /// Generates comprehensive analysis overview for entire questionnaire
        /// </summary>
        Task<QuestionnaireAnalysisOverview> GenerateQuestionnaireOverviewAsync(int questionnaireId);

        /// <summary>
        /// Batch processes multiple responses efficiently
        /// </summary>
        Task<List<ResponseAnalysisResult>> BatchAnalyzeResponsesAsync(List<AnalysisRequest> requests);

        #endregion

        #region Mental Health Specific Methods

        /// <summary>
        /// Identifies responses requiring immediate attention (high risk)
        /// </summary>
        Task<List<ResponseAnalysisResult>> IdentifyHighRiskResponsesAsync(int questionnaireId);

        /// <summary>
        /// Generates mental health trends across time periods
        /// </summary>
        Task<List<WorkplaceInsight>> AnalyzeMentalHealthTrendsAsync(int questionnaireId, DateTime fromDate, DateTime toDate);

        /// <summary>
        /// Compares mental health metrics between departments/teams
        /// </summary>
        Task<Dictionary<string, QuestionnaireAnalysisOverview>> CompareTeamMentalHealthAsync(int questionnaireId, List<string> teamIdentifiers);

        /// <summary>
        /// Generates intervention recommendations based on overall analysis
        /// </summary>
        Task<List<WorkplaceInsight>> GenerateInterventionRecommendationsAsync(int questionnaireId);

        #endregion

        #region Reporting Methods

        /// <summary>
        /// Creates detailed analysis report for specific questionnaire
        /// </summary>
        Task<string> GenerateDetailedAnalysisReportAsync(int questionnaireId);

        /// <summary>
        /// Generates anonymized data export for further analysis
        /// </summary>
        Task<List<ResponseAnalysisResult>> ExportAnonymizedAnalysisAsync(int questionnaireId);

        /// <summary>
        /// Creates management dashboard summary
        /// </summary>
        Task<QuestionnaireAnalysisOverview> GenerateManagementDashboardAsync(int questionnaireId);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Tests connection to Azure Language Service
        /// </summary>
        Task<bool> TestAzureLanguageServiceConnectionAsync();

        /// <summary>
        /// Tests connection to Azure OpenAI Service
        /// </summary>
        Task<bool> TestAzureOpenAIConnectionAsync();

        /// <summary>
        /// Validates analysis request before processing
        /// </summary>
        Task<bool> ValidateAnalysisRequestAsync(AnalysisRequest request);

        /// <summary>
        /// Gets analysis service health status
        /// </summary>
        Task<Dictionary<string, bool>> GetServiceHealthStatusAsync();

        #endregion
    }
}