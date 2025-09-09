// Services/Implementation/AiAnalysisService.cs
using Azure;
using Azure.AI.OpenAI;
using Azure.AI.TextAnalytics;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Model;
using OpenAI.Chat;
using Services.AIViewModel;
using Services.Interaces;
using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Services.Implemnetation
{
    public class AiAnalysisService : IAiAnalysisService, IDisposable
    {
        private readonly TextAnalyticsClient _textAnalyticsClient;
        private readonly AzureOpenAIClient _azureOpenAIClient;
        private readonly ChatClient _chatClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiAnalysisService> _logger;
        private readonly SurveyContext _context;
        private readonly string _openAIDeploymentName;
        private bool _disposed = false;

        public AiAnalysisService(
            IConfiguration configuration,
            ILogger<AiAnalysisService> logger,
            SurveyContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;

            // Initialize Azure Language Service
            var languageEndpoint = _configuration["AzureLanguageService:Endpoint"];
            var languageKey = _configuration["AzureLanguageService:Key"];
            _textAnalyticsClient = new TextAnalyticsClient(new Uri(languageEndpoint), new AzureKeyCredential(languageKey));

            // Initialize Azure OpenAI
            var openAIEndpoint = _configuration["AzureOpenAI:Endpoint"];
            var openAIKey = _configuration["AzureOpenAI:Key"];
            _openAIDeploymentName = _configuration["AzureOpenAI:DeploymentName"];

            _azureOpenAIClient = new AzureOpenAIClient(new Uri(openAIEndpoint), new AzureKeyCredential(openAIKey));
            _chatClient = _azureOpenAIClient.GetChatClient(_openAIDeploymentName);

            _logger.LogInformation("AiAnalysisService initialized successfully");
        }

        #region Azure Language Service Methods

        public async Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return new SentimentAnalysisResult { Sentiment = "Neutral", ConfidenceScore = 0.0 };
                }

                var response = await _textAnalyticsClient.AnalyzeSentimentAsync(text);
                var sentiment = response.Value;

                return new SentimentAnalysisResult
                {
                    Sentiment = sentiment.Sentiment.ToString(),
                    ConfidenceScore = sentiment.ConfidenceScores.Positive > sentiment.ConfidenceScores.Negative
                        ? sentiment.ConfidenceScores.Positive
                        : sentiment.ConfidenceScores.Negative,
                    PositiveScore = sentiment.ConfidenceScores.Positive,
                    NegativeScore = sentiment.ConfidenceScores.Negative,
                    NeutralScore = sentiment.ConfidenceScores.Neutral,
                    AnalyzedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing sentiment for text: {Text}", text);
                throw;
            }
        }

        public async Task<KeyPhrasesResult> ExtractKeyPhrasesAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return new KeyPhrasesResult();
                }

                var response = await _textAnalyticsClient.ExtractKeyPhrasesAsync(text);
                var keyPhrases = response.Value.ToList();

                // Mental health specific categorization
                var workplaceFactors = keyPhrases.Where(phrase =>
                    phrase.Contains("work") || phrase.Contains("manager") || phrase.Contains("team") ||
                    phrase.Contains("deadline") || phrase.Contains("pressure") || phrase.Contains("colleague") ||
                    phrase.Contains("office") || phrase.Contains("meeting") || phrase.Contains("project")).ToList();

                var emotionalIndicators = keyPhrases.Where(phrase =>
                    phrase.Contains("stress") || phrase.Contains("anxious") || phrase.Contains("tired") ||
                    phrase.Contains("overwhelmed") || phrase.Contains("frustrated") || phrase.Contains("happy") ||
                    phrase.Contains("satisfied") || phrase.Contains("motivated") || phrase.Contains("burned")).ToList();

                return new KeyPhrasesResult
                {
                    KeyPhrases = keyPhrases,
                    WorkplaceFactors = workplaceFactors,
                    EmotionalIndicators = emotionalIndicators,
                    ExtractedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting key phrases for text: {Text}", text);
                throw;
            }
        }

        public async Task<string> AnonymizeTextAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return string.Empty;
                }

                var response = await _textAnalyticsClient.RecognizePiiEntitiesAsync(text);
                var piiEntities = response.Value;

                string anonymizedText = text;
                foreach (var entity in piiEntities.OrderByDescending(e => e.Offset))
                {
                    var replacement = entity.Category.ToString() switch
                    {
                        "Person" => "[NAME]",
                        "Email" => "[EMAIL]",
                        "PhoneNumber" => "[PHONE]",
                        "Address" => "[ADDRESS]",
                        _ => "[REDACTED]"
                    };

                    anonymizedText = anonymizedText.Remove(entity.Offset, entity.Length)
                        .Insert(entity.Offset, replacement);
                }

                return anonymizedText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error anonymizing text: {Text}", text);
                return text; // Return original text if anonymization fails
            }
        }

        public async Task<List<string>> DetectEntitiesAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return new List<string>();
                }

                var response = await _textAnalyticsClient.RecognizeEntitiesAsync(text);
                var entities = response.Value;

                return entities.Select(entity => $"{entity.Category}: {entity.Text}").ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting entities for text: {Text}", text);
                throw;
            }
        }

        #endregion

        #region Azure OpenAI Methods

        public async Task<MentalHealthRiskAssessment> AssessMentalHealthRiskAsync(string anonymizedText, string questionContext)
        {
            try
            {
                var prompt = $@"
As a mental health professional, analyze this workplace survey response and assess the mental health risk level.

Question Context: {questionContext}
Response: {anonymizedText}

Please provide:
1. Risk Level (Low, Moderate, High, Critical)
2. Risk Score (0.0 to 1.0)
3. Risk Indicators (specific concerns found)
4. Protective Factors (positive elements found)
5. Requires Immediate Attention (true/false)
6. Recommended Action

Respond in this JSON format:
{{
    ""riskLevel"": ""Low"",
    ""riskScore"": 0.0,
    ""riskIndicators"": [""indicator1"", ""indicator2""],
    ""protectiveFactors"": [""factor1"", ""factor2""],
    ""requiresImmediateAttention"": false,
    ""recommendedAction"": ""specific action recommendation""
}}";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a mental health professional specialized in workplace wellness. Always respond with a single valid JSON object only. No markdown, no explanations."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions()
                {
                    Temperature = 0.3f
                };

                var response = await _chatClient.CompleteChatAsync(messages, chatOptions);
                var content = response.Value.Content[0].Text;

                _logger.LogInformation("OpenAI Response: {Content}", content);

                // Parse more defensively
                var jsonDoc = ParseLenient(content, out var parseErr);
                if (jsonDoc == null)
                {
                    _logger.LogWarning("Failed to parse JSON response from OpenAI: {Err}. Raw (first 800): {Raw}",
                        parseErr, content?.Length > 800 ? content[..800] : content);

                    return new MentalHealthRiskAssessment
                    {
                        RiskLevel = RiskLevel.Moderate,
                        RiskScore = 0.5,
                        RiskIndicators = new List<string> { "Unable to parse AI response" },
                        ProtectiveFactors = new List<string>(),
                        RequiresImmediateAttention = false,
                        RecommendedAction = "Manual review recommended due to analysis error",
                        AssessedAt = DateTime.UtcNow
                    };
                }

                var root = jsonDoc.RootElement;

                return new MentalHealthRiskAssessment
                {
                    RiskLevel = Enum.TryParse<RiskLevel>(root.TryGetProperty("riskLevel", out var rl) ? (rl.GetString() ?? "Low") : "Low", out var riskLevel)
                        ? riskLevel : RiskLevel.Low,
                    RiskScore = root.TryGetProperty("riskScore", out var scoreProperty)
                        ? TryGetDoubleFlexible(scoreProperty, out var d) ? d : 0.0
                        : 0.0,
                    RiskIndicators = root.TryGetProperty("riskIndicators", out var indicatorsProperty)
                        ? indicatorsProperty.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                        : new List<string>(),
                    ProtectiveFactors = root.TryGetProperty("protectiveFactors", out var factorsProperty)
                        ? factorsProperty.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                        : new List<string>(),
                    RequiresImmediateAttention = root.TryGetProperty("requiresImmediateAttention", out var attentionProperty) &&
                                                TryGetBoolFlexible(attentionProperty, out var b) && b,
                    RecommendedAction = root.TryGetProperty("recommendedAction", out var actionProperty) ? (actionProperty.GetString() ?? "") : "",
                    AssessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assessing mental health risk for text: {Text}", anonymizedText);

                // Return a safe default assessment instead of throwing
                return new MentalHealthRiskAssessment
                {
                    RiskLevel = RiskLevel.Moderate,
                    RiskScore = 0.5,
                    RiskIndicators = new List<string> { $"Analysis error: {ex.Message}" },
                    ProtectiveFactors = new List<string>(),
                    RequiresImmediateAttention = false,
                    RecommendedAction = "Manual review recommended due to system error",
                    AssessedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<List<WorkplaceInsight>> GenerateWorkplaceInsightsAsync(string anonymizedText, string questionContext)
        {
            try
            {
                var prompt = $@"
Analyze this workplace survey response and identify specific workplace insights and intervention recommendations.

Question Context: {questionContext}
Response: {anonymizedText}

Identify workplace issues and provide specific, actionable intervention recommendations. Focus on:
- Work-life balance issues
- Management/leadership concerns
- Team dynamics problems
- Workload and stress factors
- Communication issues
- Organizational culture problems

Respond in this JSON format:
{{
  ""insights"": [
    {{
      ""category"": ""category name"",
      ""issue"": ""specific issue identified"",
      ""recommendedIntervention"": ""specific action to take"",
      ""priority"": 1,
      ""affectedAreas"": [""area1"", ""area2""]
    }}
  ]
}}";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a workplace mental health consultant. Always respond with a SINGLE valid JSON object ONLY. No markdown, no code fences, no explanations."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions()
                {
                    Temperature = 0.4f
                };

                var response = await _chatClient.CompleteChatAsync(messages, chatOptions);
                var content = response.Value.Content[0].Text;

                var jsonDoc = ParseLenient(content, out var parseErr);
                if (jsonDoc == null)
                {
                    _logger.LogWarning("Insights JSON parse failed: {Err}. Raw (first 800): {Raw}",
                        parseErr, content?.Length > 800 ? content[..800] : content);

                    // Fail soft (avoid crashing controller)
                    return new List<WorkplaceInsight>();
                }

                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("insights", out var insightsElement) || insightsElement.ValueKind != JsonValueKind.Array)
                {
                    // No insights array — return empty rather than throw
                    return new List<WorkplaceInsight>();
                }

                var workplaceInsights = new List<WorkplaceInsight>();

                foreach (var insight in insightsElement.EnumerateArray())
                {
                    // Be defensive with each field
                    string category = insight.TryGetProperty("category", out var cat) ? (cat.GetString() ?? "") : "";
                    string issue = insight.TryGetProperty("issue", out var iss) ? (iss.GetString() ?? "") : "";
                    string recommended = insight.TryGetProperty("recommendedIntervention", out var rec) ? (rec.GetString() ?? "") : "";
                    int priority = 3;
                    if (insight.TryGetProperty("priority", out var pr))
                    {
                        if (pr.ValueKind == JsonValueKind.Number && pr.TryGetInt32(out var pInt)) priority = pInt;
                        else if (pr.ValueKind == JsonValueKind.String && int.TryParse(pr.GetString(), out var pStr)) priority = pStr;
                    }
                    var affectedAreas = new List<string>();
                    if (insight.TryGetProperty("affectedAreas", out var aa) && aa.ValueKind == JsonValueKind.Array)
                    {
                        affectedAreas = aa.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    }

                    workplaceInsights.Add(new WorkplaceInsight
                    {
                        Category = category,
                        Issue = issue,
                        RecommendedIntervention = recommended,
                        Priority = priority,
                        AffectedAreas = affectedAreas,
                        IdentifiedAt = DateTime.UtcNow
                    });
                }

                return workplaceInsights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating workplace insights for text: {Text}", anonymizedText);
                // Fail soft to prevent controller crash
                return new List<WorkplaceInsight>();
            }
        }

        public async Task<string> GenerateExecutiveSummaryAsync(List<ResponseAnalysisResult> analysisResults)
        {
            try
            {
                var totalResponses = analysisResults.Count;
                var highRiskCount = analysisResults.Count(r => r.RiskAssessment?.RiskLevel >= RiskLevel.High);
                var positiveResponses = analysisResults.Count(r => r.SentimentAnalysis?.Sentiment == "Positive");

                var commonIssues = analysisResults
                    .SelectMany(r => r.Insights)
                    .GroupBy(i => i.Category)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => $"{g.Key}: {g.Count()} instances")
                    .ToList();

                var prompt = $@"
Create an executive summary for a workplace mental health survey analysis.

Survey Statistics:
- Total Responses: {totalResponses}
- High Risk Responses: {highRiskCount}
- Positive Responses: {positiveResponses}
- Most Common Issues: {string.Join(", ", commonIssues)}

Create a comprehensive executive summary that includes:
1. Overall mental health status
2. Key findings and trends
3. Areas of concern
4. Positive indicators
5. Immediate action items
6. Long-term recommendations

Keep it professional, actionable, and suitable for senior management.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an executive consultant specializing in workplace mental health reporting."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions()
                {
                    Temperature = 0.3f
                };

                var response = await _chatClient.CompleteChatAsync(messages, chatOptions);
                return response.Value.Content[0].Text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating executive summary");
                throw;
            }
        }

        public async Task<List<string>> CategorizeResponseAsync(string anonymizedText)
        {
            try
            {
                var prompt = $@"
Categorize this workplace mental health survey response into relevant themes.

Response: {anonymizedText}

Choose from these categories (select all that apply):
- Work-Life Balance
- Workload Management
- Team Dynamics
- Leadership/Management
- Communication Issues
- Job Satisfaction
- Stress Management
- Career Development
- Work Environment
- Organizational Culture
- Mental Health Support
- Burnout Prevention

Respond with a JSON array of applicable categories: [""category1"", ""category2""]";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are a mental health professional categorizing workplace responses. Always respond with a JSON array only."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions()
                {
                    Temperature = 0.2f
                };

                var response = await _chatClient.CompleteChatAsync(messages, chatOptions);
                var content = response.Value.Content[0].Text;

                // lenient array parse
                var categories = DeserializeLenient<List<string>>(content, out var err) ?? new List<string>();
                if (!string.IsNullOrEmpty(err))
                {
                    _logger.LogWarning("Category JSON parse failed: {Err}. Raw (first 800): {Raw}",
                        err, content?.Length > 800 ? content[..800] : content);
                }

                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error categorizing response: {Text}", anonymizedText);
                throw;
            }
        }

        #endregion

        #region Combined Analysis Methods

        public async Task<ResponseAnalysisResult> AnalyzeCompleteResponseAsync(AnalysisRequest request)
        {
            try
            {
                var result = new ResponseAnalysisResult
                {
                    ResponseId = request.ResponseId,
                    QuestionId = request.QuestionId,
                    QuestionText = request.QuestionText,
                    ResponseText = request.ResponseText,
                    AnalyzedAt = DateTime.UtcNow
                };

                // Step 1: Anonymize the text
                result.AnonymizedResponseText = await AnonymizeTextAsync(request.ResponseText);

                // Step 2: Azure Language Service Analysis
                if (request.IncludeSentimentAnalysis)
                {
                    result.SentimentAnalysis = await AnalyzeSentimentAsync(result.AnonymizedResponseText);
                }

                if (request.IncludeKeyPhraseExtraction)
                {
                    result.KeyPhrases = await ExtractKeyPhrasesAsync(result.AnonymizedResponseText);
                }

                // Step 3: Azure OpenAI Analysis
                if (request.IncludeRiskAssessment)
                {
                    result.RiskAssessment = await AssessMentalHealthRiskAsync(result.AnonymizedResponseText, request.QuestionText);
                }

                if (request.IncludeWorkplaceInsights)
                {
                    result.Insights = await GenerateWorkplaceInsightsAsync(result.AnonymizedResponseText, request.QuestionText);
                }

                result.IsAnalysisComplete = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in complete response analysis for ResponseId: {ResponseId}", request.ResponseId);
                throw;
            }
        }

        public async Task<List<ResponseAnalysisResult>> AnalyzeQuestionResponsesAsync(int questionId, List<AnalysisRequest> requests)
        {
            var results = new List<ResponseAnalysisResult>();

            foreach (var request in requests)
            {
                try
                {
                    var result = await AnalyzeCompleteResponseAsync(request);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing response {ResponseId} for question {QuestionId}", request.ResponseId, questionId);
                    // Continue with other responses even if one fails
                }
            }

            return results;
        }

        public async Task<QuestionnaireAnalysisOverview> GenerateQuestionnaireOverviewAsync(int questionnaireId)
        {
            try
            {
                // Get all responses for this questionnaire
                var responses = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                    .ThenInclude(rd => rd.Question)
                    .Where(r => r.QuestionnaireId == questionnaireId)
                    .ToListAsync();

                if (!responses.Any())
                {
                    return new QuestionnaireAnalysisOverview
                    {
                        QuestionnaireId = questionnaireId,
                        QuestionnaireTitle = "No responses found",
                        TotalResponses = 0
                    };
                }

                // Analyze text responses
                var analysisResults = new List<ResponseAnalysisResult>();
                foreach (var response in responses)
                {
                    foreach (var detail in response.ResponseDetails)
                    {
                        string responseText = "";

                        // Handle text-based questions (existing)
                        if (!string.IsNullOrWhiteSpace(detail.TextResponse))
                        {
                            responseText = detail.TextResponse;
                        }
                        // Handle CheckBox questions (NEW)
                        else if (detail.QuestionType == QuestionType.CheckBox && detail.ResponseAnswers.Any())
                        {
                            var selectedAnswers = detail.ResponseAnswers
                                .Select(ra => detail.Question.Answers.FirstOrDefault(a => a.Id == ra.AnswerId)?.Text)
                                .Where(text => !string.IsNullOrEmpty(text))
                                .ToList();

                            responseText = $"Multiple Selection Question: {detail.Question.Text}\nSelected Options: {string.Join(", ", selectedAnswers)}\nAnalyze these selected workplace factors for mental health implications and patterns.";
                        }

                        // Create analysis request for ALL supported responses
                        if (!string.IsNullOrEmpty(responseText))
                        {
                            var request = new AnalysisRequest
                            {
                                ResponseId = response.Id,
                                QuestionId = detail.QuestionId,
                                ResponseText = responseText,
                                QuestionText = detail.Question?.Text ?? ""
                            };

                            var analysisResult = await AnalyzeCompleteResponseAsync(request);
                            analysisResults.Add(analysisResult);
                        }
                    }
                }

                // Generate overview
                var overview = new QuestionnaireAnalysisOverview
                {
                    QuestionnaireId = questionnaireId,
                    QuestionnaireTitle = responses.First().Questionnaire?.Title ?? "Unknown",
                    TotalResponses = responses.Count,
                    AnalyzedResponses = analysisResults.Count,
                    LastAnalyzedAt = DateTime.UtcNow
                };

                if (analysisResults.Any())
                {
                    // Calculate sentiment distribution
                    var sentimentResults = analysisResults.Where(r => r.SentimentAnalysis != null).ToList();
                    if (sentimentResults.Any())
                    {
                        overview.OverallPositiveSentiment = sentimentResults.Average(r => r.SentimentAnalysis!.PositiveScore);
                        overview.OverallNegativeSentiment = sentimentResults.Average(r => r.SentimentAnalysis!.NegativeScore);
                        overview.OverallNeutralSentiment = sentimentResults.Average(r => r.SentimentAnalysis!.NeutralScore);
                    }

                    // Calculate risk distribution
                    var riskResults = analysisResults.Where(r => r.RiskAssessment != null).ToList();
                    if (riskResults.Any())
                    {
                        overview.LowRiskResponses = riskResults.Count(r => r.RiskAssessment!.RiskLevel == RiskLevel.Low);
                        overview.ModerateRiskResponses = riskResults.Count(r => r.RiskAssessment!.RiskLevel == RiskLevel.Moderate);
                        overview.HighRiskResponses = riskResults.Count(r => r.RiskAssessment!.RiskLevel == RiskLevel.High);
                        overview.CriticalRiskResponses = riskResults.Count(r => r.RiskAssessment!.RiskLevel == RiskLevel.Critical);
                    }

                    // Get top workplace issues
                    overview.TopWorkplaceIssues = analysisResults
                        .SelectMany(r => r.Insights)
                        .GroupBy(i => i.Category)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => g.First())
                        .ToList();

                    // Get common key phrases
                    overview.MostCommonKeyPhrases = analysisResults
                        .Where(r => r.KeyPhrases != null)
                        .SelectMany(r => r.KeyPhrases!.KeyPhrases)
                        .GroupBy(k => k)
                        .OrderByDescending(g => g.Count())
                        .Take(10)
                        .Select(g => g.Key)
                        .ToList();

                    // Generate executive summary
                    overview.ExecutiveSummary = await GenerateExecutiveSummaryAsync(analysisResults);
                }

                return overview;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating questionnaire overview for QuestionnaireId: {QuestionnaireId}", questionnaireId);
                throw;
            }
        }

        public async Task<List<ResponseAnalysisResult>> BatchAnalyzeResponsesAsync(List<AnalysisRequest> requests)
        {
            var results = new List<ResponseAnalysisResult>();
            var tasks = new List<Task<ResponseAnalysisResult>>();

            // Process in batches to avoid overwhelming the API
            const int batchSize = 5;
            for (int i = 0; i < requests.Count; i += batchSize)
            {
                var batch = requests.Skip(i).Take(batchSize);
                foreach (var request in batch)
                {
                    tasks.Add(AnalyzeCompleteResponseAsync(request));
                }

                // Wait for current batch to complete before starting next
                var batchResults = await Task.WhenAll(tasks);
                results.AddRange(batchResults);
                tasks.Clear();

                // Small delay between batches to respect API limits
                await Task.Delay(1000);
            }

            return results;
        }

        #endregion

        #region Mental Health Specific Methods

        public async Task<List<ResponseAnalysisResult>> IdentifyHighRiskResponsesAsync(int questionnaireId)
        {
            try
            {
                var overview = await GenerateQuestionnaireOverviewAsync(questionnaireId);
                var allResults = await ExportAnonymizedAnalysisAsync(questionnaireId);

                return allResults
                    .Where(r => r.RiskAssessment != null &&
                           (r.RiskAssessment.RiskLevel >= RiskLevel.High || r.RiskAssessment.RequiresImmediateAttention))
                    .OrderByDescending(r => r.RiskAssessment!.RiskScore)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error identifying high risk responses for QuestionnaireId: {QuestionnaireId}", questionnaireId);
                throw;
            }
        }

        public async Task<List<WorkplaceInsight>> AnalyzeMentalHealthTrendsAsync(int questionnaireId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                var responses = await _context.Responses
                    .Include(r => r.ResponseDetails)
                    .ThenInclude(rd => rd.Question)
                    .Where(r => r.QuestionnaireId == questionnaireId &&
                               r.SubmissionDate >= fromDate &&
                               r.SubmissionDate <= toDate)
                    .OrderBy(r => r.SubmissionDate)
                    .ToListAsync();

                // This would typically involve more complex trend analysis
                // For now, return general insights
                var insights = new List<WorkplaceInsight>
                {
                    new WorkplaceInsight
                    {
                        Category = "Trend Analysis",
                        Issue = $"Analysis of {responses.Count} responses from {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}",
                        RecommendedIntervention = "Regular monitoring and follow-up assessments recommended",
                        Priority = 3,
                        IdentifiedAt = DateTime.UtcNow
                    }
                };

                return insights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing mental health trends for QuestionnaireId: {QuestionnaireId}", questionnaireId);
                throw;
            }
        }

        public async Task<Dictionary<string, QuestionnaireAnalysisOverview>> CompareTeamMentalHealthAsync(int questionnaireId, List<string> teamIdentifiers)
        {
            // This would require team identification in responses
            // For now, return a basic implementation
            var result = new Dictionary<string, QuestionnaireAnalysisOverview>();

            foreach (var team in teamIdentifiers)
            {
                result[team] = await GenerateQuestionnaireOverviewAsync(questionnaireId);
            }

            return result;
        }

        public async Task<List<WorkplaceInsight>> GenerateInterventionRecommendationsAsync(int questionnaireId)
        {
            try
            {
                var overview = await GenerateQuestionnaireOverviewAsync(questionnaireId);
                return overview.TopWorkplaceIssues;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating intervention recommendations for QuestionnaireId: {QuestionnaireId}", questionnaireId);
                throw;
            }
        }

        #endregion

        #region Reporting Methods

        public async Task<string> GenerateDetailedAnalysisReportAsync(int questionnaireId)
        {
            try
            {
                var overview = await GenerateQuestionnaireOverviewAsync(questionnaireId);
                var highRiskResponses = await IdentifyHighRiskResponsesAsync(questionnaireId);

                var report = new StringBuilder();
                report.AppendLine($"# Mental Health Analysis Report");
                report.AppendLine($"**Questionnaire:** {overview.QuestionnaireTitle}");
                report.AppendLine($"**Analysis Date:** {DateTime.Now:yyyy-MM-dd HH:mm}");
                report.AppendLine($"**Total Responses:** {overview.TotalResponses}");
                report.AppendLine($"**Analyzed Responses:** {overview.AnalyzedResponses}");
                report.AppendLine();

                report.AppendLine("## Executive Summary");
                report.AppendLine(overview.ExecutiveSummary);
                report.AppendLine();

                report.AppendLine("## Risk Distribution");
                report.AppendLine($"- Low Risk: {overview.LowRiskResponses}");
                report.AppendLine($"- Moderate Risk: {overview.ModerateRiskResponses}");
                report.AppendLine($"- High Risk: {overview.HighRiskResponses}");
                report.AppendLine($"- Critical Risk: {overview.CriticalRiskResponses}");
                report.AppendLine();

                if (highRiskResponses.Any())
                {
                    report.AppendLine("## High Risk Responses Requiring Attention");
                    report.AppendLine($"Found {highRiskResponses.Count} responses requiring immediate attention.");
                    report.AppendLine();
                }

                report.AppendLine("## Top Workplace Issues");
                foreach (var issue in overview.TopWorkplaceIssues.Take(5))
                {
                    report.AppendLine($"- **{issue.Category}:** {issue.Issue}");
                    report.AppendLine($"  - Recommended Action: {issue.RecommendedIntervention}");
                }

                return report.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating detailed analysis report for QuestionnaireId: {QuestionnaireId}", questionnaireId);
                throw;
            }
        }

        public async Task<List<ResponseAnalysisResult>> ExportAnonymizedAnalysisAsync(int questionnaireId)
        {
            try
            {
                var responses = await _context.Responses
                    .Include(r => r.ResponseDetails)
                    .ThenInclude(rd => rd.Question)
                    .Where(r => r.QuestionnaireId == questionnaireId)
                    .ToListAsync();

                var results = new List<ResponseAnalysisResult>();

                foreach (var response in responses)
                {
                    foreach (var detail in response.ResponseDetails)
                    {
                        string responseText = "";

                        // Handle text-based questions
                        if (!string.IsNullOrWhiteSpace(detail.TextResponse))
                        {
                            responseText = detail.TextResponse;
                        }
                        // Handle CheckBox questions
                        else if (detail.QuestionType == QuestionType.CheckBox && detail.ResponseAnswers.Any())
                        {
                            var selectedAnswers = detail.ResponseAnswers
                                .Select(ra => detail.Question.Answers.FirstOrDefault(a => a.Id == ra.AnswerId)?.Text)
                                .Where(text => !string.IsNullOrEmpty(text))
                                .ToList();

                            responseText = $"Multiple Selection Question: {detail.Question.Text}\nSelected Options: {string.Join(", ", selectedAnswers)}\nAnalyze these selected workplace factors for mental health implications and patterns.";
                        }

                        // Create analysis request if we have text to analyze
                        if (!string.IsNullOrEmpty(responseText))
                        {
                            var request = new AnalysisRequest
                            {
                                ResponseId = response.Id,
                                QuestionId = detail.QuestionId,
                                ResponseText = responseText,
                                QuestionText = detail.Question?.Text ?? ""
                            };

                            var result = await AnalyzeCompleteResponseAsync(request);
                            results.Add(result);
                        }
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting anonymized analysis for QuestionnaireId: {QuestionnaireId}", questionnaireId);
                throw;
            }
        }

        public async Task<QuestionnaireAnalysisOverview> GenerateManagementDashboardAsync(int questionnaireId)
        {
            return await GenerateQuestionnaireOverviewAsync(questionnaireId);
        }

        #endregion

        #region Utility Methods

        public async Task<bool> TestAzureLanguageServiceConnectionAsync()
        {
            try
            {
                await _textAnalyticsClient.AnalyzeSentimentAsync("Test connection");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Language Service connection test failed");
                return false;
            }
        }

        public async Task<bool> TestAzureOpenAIConnectionAsync()
        {
            try
            {
                var messages = new List<ChatMessage>
                {
                    new UserChatMessage("Test connection")
                };

                var chatOptions = new ChatCompletionOptions();

                await _chatClient.CompleteChatAsync(messages, chatOptions);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure OpenAI connection test failed");
                return false;
            }
        }

        public Task<bool> ValidateAnalysisRequestAsync(AnalysisRequest request)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(request.ResponseText) &&
                   request.ResponseId > 0 &&
                   request.QuestionId > 0);
        }

        public async Task<Dictionary<string, bool>> GetServiceHealthStatusAsync()
        {
            var status = new Dictionary<string, bool>();

            status["AzureLanguageService"] = await TestAzureLanguageServiceConnectionAsync();
            status["AzureOpenAI"] = await TestAzureOpenAIConnectionAsync();

            return status;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion

        #region Private JSON Helpers (no new models)

        private static readonly Regex JsonObjectRegex =
            new(@"(\{(?:[^{}]|(?<o>\{)|(?<-o>\}))*(?(o)(?!))\})",
                RegexOptions.Singleline | RegexOptions.Compiled);

        private static bool TryExtractJsonObject(string text, out string json)
        {
            json = text?.Trim() ?? "";

            // strip markdown code fences if present
            if (json.StartsWith("```"))
            {
                json = Regex.Replace(json, "^```(?:json)?\\s*|\\s*```$", "",
                                     RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();
            }

            if (json.StartsWith("{") && json.EndsWith("}"))
                return true;

            var m = JsonObjectRegex.Match(json);
            if (!m.Success) return false;

            json = m.Value;
            return true;
        }

        private static JsonDocument? ParseLenient(string content, out string? error)
        {
            error = null;

            if (!TryExtractJsonObject(content, out var jsonOnly))
            {
                error = "No JSON object found in model response.";
                return null;
            }

            var docOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            try
            {
                return JsonDocument.Parse(jsonOnly, docOptions);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        private static T? DeserializeLenient<T>(string content, out string? error)
        {
            error = null;

            if (!TryExtractJsonObject(content, out var jsonOnly))
            {
                // also try plain array (for CategorizeResponseAsync)
                var trimmed = (content ?? "").Trim();
                if (typeof(T) == typeof(List<string>) && trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    jsonOnly = trimmed;
                }
                else
                {
                    error = "No JSON object/array found in model response.";
                    return default;
                }
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            try
            {
                return JsonSerializer.Deserialize<T>(jsonOnly, options);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return default;
            }
        }

        private static bool TryGetDoubleFlexible(JsonElement el, out double value)
        {
            value = 0;
            if (el.ValueKind == JsonValueKind.Number) return el.TryGetDouble(out value);
            if (el.ValueKind == JsonValueKind.String) return double.TryParse(el.GetString(), out value);
            return false;
        }

        private static bool TryGetBoolFlexible(JsonElement el, out bool value)
        {
            value = false;
            if (el.ValueKind == JsonValueKind.True) { value = true; return true; }
            if (el.ValueKind == JsonValueKind.False) { value = false; return true; }
            if (el.ValueKind == JsonValueKind.String) return bool.TryParse(el.GetString(), out value);
            return false;
        }



        #endregion
    }
}
