using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Configuration;
using Services.Interaces;

namespace Services.Implemnetation
{
    public class AiAnalysisService : IAiAnalysisService
    {
        private readonly TextAnalyticsClient _client;

        public AiAnalysisService(IConfiguration configuration)
        {
            var endpoint = configuration["AzureLanguageService:Endpoint"];
            var key = configuration["AzureLanguageService:Key"];

            _client = new TextAnalyticsClient(new Uri(endpoint), new AzureKeyCredential(key));
        }

        public async Task<DocumentSentiment> AnalyzeSentimentAsync(string text)
        {
            var response = await _client.AnalyzeSentimentAsync(text);
            return response.Value;
        }

        public async Task<string> GetRiskAssessmentAsync(string text)
        {
            var sentiment = await AnalyzeSentimentAsync(text);

            // Simple risk assessment logic
            if (sentiment.Sentiment == TextSentiment.Negative && sentiment.ConfidenceScores.Negative > 0.8)
            {
                return "High Risk - Consider follow-up";
            }
            else if (sentiment.Sentiment == TextSentiment.Negative)
            {
                return "Medium Risk - Monitor";
            }
            else
            {
                return "Low Risk";
            }
        }

        public async Task<List<string>> ExtractKeyPhrasesAsync(string text)
        {
            var response = await _client.ExtractKeyPhrasesAsync(text);
            return response.Value.ToList();
        }
    }
}
