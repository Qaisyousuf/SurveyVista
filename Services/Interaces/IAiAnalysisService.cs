using Azure.AI.TextAnalytics;

namespace Services.Interaces
{
    public interface IAiAnalysisService
    {
        Task<DocumentSentiment> AnalyzeSentimentAsync(string text);
        Task<string> GetRiskAssessmentAsync(string text);
        Task<List<string>> ExtractKeyPhrasesAsync(string text);
    }
}
