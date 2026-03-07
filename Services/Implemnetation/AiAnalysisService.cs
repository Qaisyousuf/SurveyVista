// Services/Implementation/AiAnalysisService.cs
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Model;
using Services.AIViewModel;
using Services.Interaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Services.Implemnetation
{
    public class AiAnalysisService : IAiAnalysisService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiAnalysisService> _logger;
        private readonly SurveyContext _context;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly SemaphoreSlim _rateLimiter;
        private bool _disposed;

        private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";
        private const int MaxRetries = 3;
        private const int BaseDelayMs = 1000;

        public AiAnalysisService(
            IConfiguration configuration,
            ILogger<AiAnalysisService> logger,
            SurveyContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;

            _apiKey = _configuration["Claude:ApiKey"]
                ?? throw new InvalidOperationException("Claude:ApiKey is not configured in appsettings.json");
            _model = _configuration["Claude:Model"] ?? "claude-sonnet-4-20250514";

            // Configure HttpClient
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Rate limiter: max 5 concurrent requests to Claude
            _rateLimiter = new SemaphoreSlim(5, 5);

            _logger.LogInformation("AiAnalysisService initialized with Claude API (model: {Model})", _model);
        }

        #region Core Claude API Communication

        /// <summary>
        /// Sends a message to Claude API with retry logic and rate limiting.
        /// </summary>
        private async Task<string> SendClaudeRequestAsync(string systemPrompt, string userPrompt, float temperature = 0.3f, int maxTokens = 2048)
        {
            await _rateLimiter.WaitAsync();
            try
            {
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        var requestBody = new
                        {
                            model = _model,
                            max_tokens = maxTokens,
                            temperature = temperature,
                            system = systemPrompt,
                            messages = new[]
                            {
                                new { role = "user", content = userPrompt }
                            }
                        };

                        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var response = await _httpClient.PostAsync(ClaudeApiUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseJson = await response.Content.ReadAsStringAsync();
                            var doc = JsonDocument.Parse(responseJson);

                            // Extract text from Claude's response content array
                            if (doc.RootElement.TryGetProperty("content", out var contentArray) &&
                                contentArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var block in contentArray.EnumerateArray())
                                {
                                    if (block.TryGetProperty("type", out var type) &&
                                        type.GetString() == "text" &&
                                        block.TryGetProperty("text", out var text))
                                    {
                                        return text.GetString() ?? string.Empty;
                                    }
                                }
                            }

                            _logger.LogWarning("Claude response had no text content block. Raw: {Raw}",
                                responseJson.Length > 500 ? responseJson[..500] : responseJson);
                            return string.Empty;
                        }

                        // Handle rate limiting (429)
                        if ((int)response.StatusCode == 429)
                        {
                            var delay = BaseDelayMs * (int)Math.Pow(2, attempt - 1);
                            _logger.LogWarning("Claude API rate limited (attempt {Attempt}/{Max}). Retrying in {Delay}ms",
                                attempt, MaxRetries, delay);
                            await Task.Delay(delay);
                            continue;
                        }

                        // Handle overloaded (529)
                        if ((int)response.StatusCode == 529)
                        {
                            var delay = BaseDelayMs * (int)Math.Pow(2, attempt);
                            _logger.LogWarning("Claude API overloaded (attempt {Attempt}/{Max}). Retrying in {Delay}ms",
                                attempt, MaxRetries, delay);
                            await Task.Delay(delay);
                            continue;
                        }

                        // Other errors
                        var errorBody = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Claude API error {StatusCode} (attempt {Attempt}): {Error}",
                            (int)response.StatusCode, attempt, errorBody.Length > 500 ? errorBody[..500] : errorBody);

                        if (attempt == MaxRetries)
                            throw new HttpRequestException($"Claude API returned {(int)response.StatusCode} after {MaxRetries} attempts");

                        await Task.Delay(BaseDelayMs * attempt);
                    }
                    catch (TaskCanceledException) when (attempt < MaxRetries)
                    {
                        _logger.LogWarning("Claude API timeout (attempt {Attempt}/{Max})", attempt, MaxRetries);
                        await Task.Delay(BaseDelayMs * attempt);
                    }
                    catch (HttpRequestException) when (attempt < MaxRetries)
                    {
                        _logger.LogWarning("Claude API connection error (attempt {Attempt}/{Max})", attempt, MaxRetries);
                        await Task.Delay(BaseDelayMs * attempt);
                    }
                }

                throw new HttpRequestException("Claude API request failed after all retry attempts");
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        #endregion

        #region Core Analysis Methods

        public async Task<SentimentAnalysisResult> AnalyzeSentimentAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return new SentimentAnalysisResult { Sentiment = "Neutral", ConfidenceScore = 0.0 };

                var systemPrompt = @"You are a sentiment analysis engine for workplace mental health surveys. 
Respond ONLY with a valid JSON object. No markdown, no explanation, no code fences.";

                var userPrompt = $@"Analyze the sentiment of this workplace survey response:

""{text}""

Return this exact JSON structure:
{{
  ""sentiment"": ""Positive"",
  ""confidenceScore"": 0.85,
  ""positiveScore"": 0.85,
  ""negativeScore"": 0.10,
  ""neutralScore"": 0.05
}}

Rules:
- sentiment must be exactly ""Positive"", ""Negative"", or ""Neutral""
- All scores must be between 0.0 and 1.0
- Scores should approximately sum to 1.0
- confidenceScore = the highest of the three scores";

                var response = await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.1f);
                var doc = ParseLenientJson(response);

                if (doc == null)
                {
                    _logger.LogWarning("Failed to parse sentiment response. Raw: {Raw}", Truncate(response));
                    return new SentimentAnalysisResult { Sentiment = "Neutral", ConfidenceScore = 0.5 };
                }

                var root = doc.RootElement;
                return new SentimentAnalysisResult
                {
                    Sentiment = GetStringProp(root, "sentiment", "Neutral"),
                    ConfidenceScore = GetDoubleProp(root, "confidenceScore", 0.5),
                    PositiveScore = GetDoubleProp(root, "positiveScore", 0.0),
                    NegativeScore = GetDoubleProp(root, "negativeScore", 0.0),
                    NeutralScore = GetDoubleProp(root, "neutralScore", 0.0),
                    AnalyzedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing sentiment");
                return new SentimentAnalysisResult { Sentiment = "Neutral", ConfidenceScore = 0.0 };
            }
        }

        public async Task<KeyPhrasesResult> ExtractKeyPhrasesAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return new KeyPhrasesResult();

                var systemPrompt = @"You are a key phrase extraction engine for workplace mental health surveys.
Respond ONLY with a valid JSON object. No markdown, no explanation, no code fences.";

                var userPrompt = $@"Extract key phrases from this workplace survey response:

""{text}""

Return this exact JSON structure:
{{
  ""keyPhrases"": [""phrase1"", ""phrase2""],
  ""workplaceFactors"": [""factor1"", ""factor2""],
  ""emotionalIndicators"": [""indicator1"", ""indicator2""]
}}

Rules:
- keyPhrases: all significant noun phrases and concepts (5-15 phrases)
- workplaceFactors: only phrases related to work environment, management, teams, deadlines, meetings, projects, colleagues, workload
- emotionalIndicators: only phrases indicating emotional state (stress, anxiety, satisfaction, motivation, frustration, burnout, happiness, exhaustion)";

                var response = await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.2f);
                var doc = ParseLenientJson(response);

                if (doc == null)
                {
                    _logger.LogWarning("Failed to parse key phrases response. Raw: {Raw}", Truncate(response));
                    return new KeyPhrasesResult();
                }

                var root = doc.RootElement;
                return new KeyPhrasesResult
                {
                    KeyPhrases = GetStringArray(root, "keyPhrases"),
                    WorkplaceFactors = GetStringArray(root, "workplaceFactors"),
                    EmotionalIndicators = GetStringArray(root, "emotionalIndicators"),
                    ExtractedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting key phrases");
                return new KeyPhrasesResult();
            }
        }

        public async Task<string> AnonymizeTextAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return string.Empty;

                var systemPrompt = @"You are a PII anonymization engine. Your job is to replace personally identifiable information in text with placeholder tags. Return ONLY the anonymized text — nothing else.";

                var userPrompt = $@"Anonymize this text by replacing PII with these tags:
- Person names → [NAME]
- Email addresses → [EMAIL]
- Phone numbers → [PHONE]
- Physical addresses → [ADDRESS]
- Company names (if identifying specific small company) → [ORG]
- Any other identifying info → [REDACTED]

Keep all non-PII text exactly as-is. Do NOT add explanations or formatting.

Text: ""{text}""";

                var response = await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.0f);

                // Clean up: remove any quotes Claude might wrap the response in
                var cleaned = response.Trim().Trim('"');
                return string.IsNullOrWhiteSpace(cleaned) ? text : cleaned;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error anonymizing text");
                return text; // Return original if anonymization fails
            }
        }

        public async Task<List<string>> DetectEntitiesAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    return new List<string>();

                var systemPrompt = @"You are a named entity recognition engine.
Respond ONLY with a valid JSON array. No markdown, no explanation.";

                var userPrompt = $@"Detect named entities in this text and categorize them:

""{text}""

Return a JSON array of strings in ""Category: Entity"" format:
[""Person: John Smith"", ""Organization: Acme Corp"", ""Location: New York"", ""Role: Manager""]

Categories: Person, Organization, Location, Role, Department, Date, Event";

                var response = await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.1f);
                return DeserializeLenient<List<string>>(response) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting entities");
                return new List<string>();
            }
        }

        #endregion

        #region Risk Assessment Methods

        public async Task<MentalHealthRiskAssessment> AssessMentalHealthRiskAsync(string anonymizedText, string questionContext)
        {
            try
            {
                var systemPrompt = @"You are a clinical workplace psychologist specializing in occupational mental health risk assessment. You analyze employee survey responses to identify mental health risk factors.

CRITICAL: Respond ONLY with a single valid JSON object. No markdown, no code fences, no explanation text.";

                var userPrompt = $@"Assess the mental health risk level of this anonymized workplace survey response.

Survey Question: {questionContext}
Employee Response: {anonymizedText}

Evaluate for:
- Signs of burnout, exhaustion, or chronic stress
- Anxiety or depression indicators
- Work-life balance deterioration
- Social withdrawal or conflict
- Self-harm or crisis indicators (escalate to Critical)
- Protective factors (coping mechanisms, social support, positive outlook)

Return this exact JSON structure:
{{
  ""riskLevel"": ""Low"",
  ""riskScore"": 0.25,
  ""riskIndicators"": [""specific concern 1"", ""specific concern 2""],
  ""protectiveFactors"": [""positive factor 1"", ""positive factor 2""],
  ""requiresImmediateAttention"": false,
  ""recommendedAction"": ""specific actionable recommendation""
}}

Rules:
- riskLevel: exactly ""Low"", ""Moderate"", ""High"", or ""Critical""
- riskScore: 0.0 (no risk) to 1.0 (maximum risk)
  - Low: 0.0–0.25, Moderate: 0.26–0.50, High: 0.51–0.75, Critical: 0.76–1.0
- requiresImmediateAttention: true ONLY for Critical or if self-harm/crisis indicators present
- recommendedAction: professional, specific, actionable (not generic)";

                var response = await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.2f);
                var doc = ParseLenientJson(response);

                if (doc == null)
                {
                    _logger.LogWarning("Failed to parse risk assessment. Raw: {Raw}", Truncate(response));
                    return DefaultRiskAssessment("Unable to parse AI response");
                }

                var root = doc.RootElement;
                var riskLevelStr = GetStringProp(root, "riskLevel", "Moderate");

                return new MentalHealthRiskAssessment
                {
                    RiskLevel = Enum.TryParse<RiskLevel>(riskLevelStr, true, out var rl) ? rl : RiskLevel.Moderate,
                    RiskScore = Math.Clamp(GetDoubleProp(root, "riskScore", 0.5), 0.0, 1.0),
                    RiskIndicators = GetStringArray(root, "riskIndicators"),
                    ProtectiveFactors = GetStringArray(root, "protectiveFactors"),
                    RequiresImmediateAttention = GetBoolProp(root, "requiresImmediateAttention", false),
                    RecommendedAction = GetStringProp(root, "recommendedAction", "Manual review recommended"),
                    AssessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assessing mental health risk");
                return DefaultRiskAssessment($"Analysis error: {ex.Message}");
            }
        }

        public async Task<List<WorkplaceInsight>> GenerateWorkplaceInsightsAsync(string anonymizedText, string questionContext)
        {
            try
            {
                var systemPrompt = @"You are a workplace mental health consultant specializing in organizational wellness intervention design.

CRITICAL: Respond ONLY with a single valid JSON object. No markdown, no code fences, no explanation.";

                var userPrompt = $@"Analyze this workplace survey response and identify actionable workplace insights.

Survey Question: {questionContext}
Employee Response: {anonymizedText}

Identify issues across these domains:
- Work-Life Balance, Workload Management, Team Dynamics
- Leadership/Management, Communication, Job Satisfaction
- Stress Management, Career Development, Work Environment
- Organizational Culture, Mental Health Support, Burnout Prevention

Return this exact JSON:
{{
  ""insights"": [
    {{
      ""category"": ""Work-Life Balance"",
      ""issue"": ""specific issue identified from response"",
      ""recommendedIntervention"": ""specific, actionable organizational intervention"",
      ""priority"": 3,
      ""affectedAreas"": [""Employee Wellbeing"", ""Productivity""]
    }}
  ]
}}

Rules:
- 1–5 insights maximum (only genuinely identified issues)
- priority: 1 (critical) to 5 (minor)
- recommendedIntervention: professional, specific, implementable by HR/management
- Do NOT invent issues not supported by the response text";

                var response = await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.3f);
                var doc = ParseLenientJson(response);

                if (doc == null)
                {
                    _logger.LogWarning("Failed to parse insights. Raw: {Raw}", Truncate(response));
                    return new List<WorkplaceInsight>();
                }

                var root = doc.RootElement;
                if (!root.TryGetProperty("insights", out var insightsEl) || insightsEl.ValueKind != JsonValueKind.Array)
                    return new List<WorkplaceInsight>();

                var insights = new List<WorkplaceInsight>();
                foreach (var item in insightsEl.EnumerateArray())
                {
                    insights.Add(new WorkplaceInsight
                    {
                        Category = GetStringProp(item, "category", "General"),
                        Issue = GetStringProp(item, "issue", ""),
                        RecommendedIntervention = GetStringProp(item, "recommendedIntervention", ""),
                        Priority = Math.Clamp(GetIntProp(item, "priority", 3), 1, 5),
                        AffectedAreas = GetStringArray(item, "affectedAreas"),
                        IdentifiedAt = DateTime.UtcNow
                    });
                }

                return insights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating workplace insights");
                return new List<WorkplaceInsight>();
            }
        }

        public async Task<string> GenerateExecutiveSummaryAsync(List<ResponseAnalysisResult> analysisResults)
        {
            try
            {
                var totalResponses = analysisResults.Count;
                var highRiskCount = analysisResults.Count(r => r.RiskAssessment?.RiskLevel >= RiskLevel.High);
                var criticalCount = analysisResults.Count(r => r.RiskAssessment?.RiskLevel == RiskLevel.Critical);
                var positiveCount = analysisResults.Count(r => r.SentimentAnalysis?.Sentiment == "Positive");
                var negativeCount = analysisResults.Count(r => r.SentimentAnalysis?.Sentiment == "Negative");

                var topCategories = analysisResults
                    .SelectMany(r => r.Insights)
                    .GroupBy(i => i.Category)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => $"{g.Key}: {g.Count()} instances")
                    .ToList();

                var topKeyPhrases = analysisResults
                    .Where(r => r.KeyPhrases != null)
                    .SelectMany(r => r.KeyPhrases!.KeyPhrases)
                    .GroupBy(k => k.ToLower())
                    .OrderByDescending(g => g.Count())
                    .Take(8)
                    .Select(g => g.Key)
                    .ToList();

                var systemPrompt = @"You are a senior organizational psychologist writing executive briefings for C-level leadership. Your writing is precise, data-driven, professional, and actionable. No fluff.";

                var userPrompt = $@"Create a professional executive summary for this workplace mental health survey analysis.

DATA:
- Total analyzed responses: {totalResponses}
- Positive sentiment: {positiveCount} ({(totalResponses > 0 ? (positiveCount * 100.0 / totalResponses).ToString("F1") : "0")}%)
- Negative sentiment: {negativeCount} ({(totalResponses > 0 ? (negativeCount * 100.0 / totalResponses).ToString("F1") : "0")}%)
- High risk responses: {highRiskCount}
- Critical risk responses: {criticalCount}
- Top workplace issues: {string.Join(", ", topCategories)}
- Recurring themes: {string.Join(", ", topKeyPhrases)}

Write the summary with these sections:
1. OVERALL ASSESSMENT (2-3 sentences, overall mental health posture)
2. KEY FINDINGS (3-5 bullet points, data-backed)
3. AREAS OF CONCERN (prioritized list with risk implications)
4. POSITIVE INDICATORS (strengths to preserve)
5. IMMEDIATE ACTIONS (3-5 specific, implementable recommendations)
6. STRATEGIC RECOMMENDATIONS (long-term organizational improvements)

Keep it under 600 words. Professional tone suitable for board presentation.";

                return await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.3f, 3000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating executive summary");
                return "Executive summary generation failed. Please retry the analysis.";
            }
        }

        public async Task<List<string>> CategorizeResponseAsync(string anonymizedText)
        {
            try
            {
                var systemPrompt = @"You are a response categorization engine for workplace surveys.
Respond ONLY with a JSON array of strings. No markdown, no explanation.";

                var userPrompt = $@"Categorize this workplace mental health response into applicable themes:

""{anonymizedText}""

Choose ALL that apply from:
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

Return as JSON array: [""Category1"", ""Category2""]";

                var response = await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.1f);
                return DeserializeLenient<List<string>>(response) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error categorizing response");
                return new List<string>();
            }
        }

        #endregion

        #region Composite Analysis Methods

        public async Task<ResponseAnalysisResult> AnalyzeCompleteResponseAsync(AnalysisRequest request)
        {
            try
            {
                // Step 1: Check if already analyzed in DB
                var cached = await LoadResponseAnalysisFromDbAsync(request.ResponseId, request.QuestionId);
                if (cached != null)
                {
                    _logger.LogInformation("Loaded cached analysis for ResponseId: {ResponseId}, QuestionId: {QuestionId}", request.ResponseId, request.QuestionId);
                    return cached;
                }

                // Step 2: Not in DB — run full Claude analysis
                _logger.LogInformation("No cached analysis found. Calling Claude API for ResponseId: {ResponseId}, QuestionId: {QuestionId}", request.ResponseId, request.QuestionId);

                var result = new ResponseAnalysisResult
                {
                    ResponseId = request.ResponseId,
                    QuestionId = request.QuestionId,
                    QuestionText = request.QuestionText,
                    ResponseText = request.ResponseText,
                    AnalyzedAt = DateTime.UtcNow
                };

                // Anonymize the text
                result.AnonymizedResponseText = await AnonymizeTextAsync(request.ResponseText);

                // Claude API calls
                if (request.IncludeSentimentAnalysis)
                {
                    result.SentimentAnalysis = await AnalyzeSentimentAsync(result.AnonymizedResponseText);
                }

                if (request.IncludeKeyPhraseExtraction)
                {
                    result.KeyPhrases = await ExtractKeyPhrasesAsync(result.AnonymizedResponseText);
                }

                if (request.IncludeRiskAssessment)
                {
                    result.RiskAssessment = await AssessMentalHealthRiskAsync(result.AnonymizedResponseText, request.QuestionText);
                }

                if (request.IncludeWorkplaceInsights)
                {
                    result.Insights = await GenerateWorkplaceInsightsAsync(result.AnonymizedResponseText, request.QuestionText);
                }

                result.IsAnalysisComplete = true;

                // Step 3: Save to DB for future use
                await SaveResponseAnalysisToDbAsync(result);

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
                    results.Add(await AnalyzeCompleteResponseAsync(request));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing response {ResponseId} for question {QuestionId}",
                        request.ResponseId, questionId);
                }
            }
            return results;
        }
        /// <summary>
        /// Builds a combined analysis text from a ResponseDetail, including both text responses
        /// and selected answer texts for checkbox/radio/multiple choice questions.
        /// </summary>
        private string BuildAnalysisText(ResponseDetail detail)
        {
            var parts = new List<string>();

            // Include text response if present
            if (!string.IsNullOrWhiteSpace(detail.TextResponse))
            {
                parts.Add(detail.TextResponse);
            }

            // Include selected answer texts for non-text questions
            if (detail.ResponseAnswers != null && detail.ResponseAnswers.Any())
            {
                foreach (var ra in detail.ResponseAnswers)
                {
                    // Navigate through the Answer entity to get the text
                    if (ra.Answer != null && !string.IsNullOrWhiteSpace(ra.Answer.Text))
                    {
                        parts.Add(ra.Answer.Text);
                    }
                }
            }

            return string.Join(". ", parts);
        }
        public async Task<QuestionnaireAnalysisOverview> GenerateQuestionnaireOverviewAsync(int questionnaireId)
        {
            try
            {
                // Get all responses — NOW including ResponseAnswers -> Answer for selected answer text
                var responses = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                            .ThenInclude(ra => ra.Answer)  // <-- KEY FIX: load Answer.Text
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

                // Analyze ALL response types (text + checkbox + radio + etc.)
                var analysisResults = new List<ResponseAnalysisResult>();
                foreach (var response in responses)
                {
                    foreach (var detail in response.ResponseDetails)
                    {
                        var analysisText = BuildAnalysisText(detail);

                        if (!string.IsNullOrWhiteSpace(analysisText))
                        {
                            var request = new AnalysisRequest
                            {
                                ResponseId = response.Id,
                                QuestionId = detail.QuestionId,
                                ResponseText = analysisText,
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

            foreach (var request in requests)
            {
                try
                {
                    // AnalyzeCompleteResponseAsync already checks DB first
                    // So cached ones return instantly, only new ones hit Claude
                    var result = await AnalyzeCompleteResponseAsync(request);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing response {ResponseId} for question {QuestionId}", request.ResponseId, request.QuestionId);
                }
            }

            return results;
        }

        #endregion

        #region Mental Health Intelligence

        public async Task<List<ResponseAnalysisResult>> IdentifyHighRiskResponsesAsync(int questionnaireId)
        {
            try
            {
                // Read from DB — no API calls
                var highRiskEntities = await _context.ResponseAnalyses
                    .Where(ra => _context.Responses
                        .Where(r => r.QuestionnaireId == questionnaireId)
                        .Select(r => r.Id)
                        .Contains(ra.ResponseId))
                    .Where(ra => ra.RiskLevel == "High" || ra.RiskLevel == "Critical" || ra.RequiresImmediateAttention)
                    .OrderByDescending(ra => ra.RiskScore)
                    .ToListAsync();

                // Convert entities to view models
                var results = new List<ResponseAnalysisResult>();
                foreach (var entity in highRiskEntities)
                {
                    var loaded = await LoadResponseAnalysisFromDbAsync(entity.ResponseId, entity.QuestionId);
                    if (loaded != null)
                    {
                        results.Add(loaded);
                    }
                }

                return results;
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

                return new List<WorkplaceInsight>
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing trends for {QuestionnaireId}", questionnaireId);
                throw;
            }
        }

        public async Task<Dictionary<string, QuestionnaireAnalysisOverview>> CompareTeamMentalHealthAsync(
            int questionnaireId, List<string> teamIdentifiers)
        {
            var result = new Dictionary<string, QuestionnaireAnalysisOverview>();
            foreach (var team in teamIdentifiers)
                result[team] = await GenerateQuestionnaireOverviewAsync(questionnaireId);
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

        #region Reporting

        public async Task<string> GenerateDetailedAnalysisReportAsync(int questionnaireId)
        {
            try
            {
                // Both of these now read from DB
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
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                            .ThenInclude(ra => ra.Answer)
                    .Where(r => r.QuestionnaireId == questionnaireId)
                    .ToListAsync();

                var results = new List<ResponseAnalysisResult>();

                foreach (var response in responses)
                {
                    foreach (var detail in response.ResponseDetails)
                    {
                        var analysisText = BuildAnalysisText(detail);

                        if (!string.IsNullOrWhiteSpace(analysisText))
                        {
                            var request = new AnalysisRequest
                            {
                                ResponseId = response.Id,
                                QuestionId = detail.QuestionId,
                                ResponseText = analysisText,
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

        #region Service Health

        public async Task<bool> TestClaudeConnectionAsync()
        {
            try
            {
                var response = await SendClaudeRequestAsync(
                    "You are a health check endpoint. Respond with exactly: OK",
                    "Health check. Respond with exactly: OK",
                    0.0f, 10);

                return !string.IsNullOrWhiteSpace(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude connection test failed");
                return false;
            }
        }

        public Task<bool> ValidateAnalysisRequestAsync(AnalysisRequest request)
        {
            return Task.FromResult(
                !string.IsNullOrWhiteSpace(request.ResponseText) &&
                request.ResponseId >= 0 &&
                request.QuestionId >= 0);
        }

        public async Task<Dictionary<string, bool>> GetServiceHealthStatusAsync()
        {
            return new Dictionary<string, bool>
            {
                ["Claude"] = await TestClaudeConnectionAsync()
            };
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Extracts analyzable text from a ResponseDetail (text responses + checkbox selections).
        /// </summary>
        private static string ExtractAnalyzableText(ResponseDetail detail)
        {
            if (!string.IsNullOrWhiteSpace(detail.TextResponse))
                return detail.TextResponse;

            if (detail.QuestionType == QuestionType.CheckBox && detail.ResponseAnswers.Any())
            {
                var selectedAnswers = detail.ResponseAnswers
                    .Select(ra => detail.Question?.Answers?.FirstOrDefault(a => a.Id == ra.AnswerId)?.Text)
                    .Where(text => !string.IsNullOrEmpty(text))
                    .ToList();

                if (selectedAnswers.Any())
                {
                    return $"Multiple Selection Question: {detail.Question?.Text}\n" +
                           $"Selected Options: {string.Join(", ", selectedAnswers)}\n" +
                           "Analyze these selected workplace factors for mental health implications and patterns.";
                }
            }

            return string.Empty;
        }

        private static MentalHealthRiskAssessment DefaultRiskAssessment(string reason)
        {
            return new MentalHealthRiskAssessment
            {
                RiskLevel = RiskLevel.Moderate,
                RiskScore = 0.5,
                RiskIndicators = new List<string> { reason },
                ProtectiveFactors = new List<string>(),
                RequiresImmediateAttention = false,
                RecommendedAction = "Manual review recommended due to analysis error",
                AssessedAt = DateTime.UtcNow
            };
        }

        private static string Truncate(string? s, int max = 800) =>
            s == null ? "" : s.Length > max ? s[..max] : s;

        #endregion

        #region JSON Parsing Helpers

        private static readonly Regex JsonObjectRegex = new(
            @"(\{(?:[^{}]|(?<o>\{)|(?<-o>\}))*(?(o)(?!))\})",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static JsonDocument? ParseLenientJson(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            var json = content.Trim();

            // Strip markdown code fences
            if (json.StartsWith("```"))
                json = Regex.Replace(json, @"^```(?:json)?\s*|\s*```$", "",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();

            // Try direct parse first
            if (json.StartsWith("{"))
            {
                try
                {
                    return JsonDocument.Parse(json, new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                }
                catch { }
            }

            // Try regex extraction
            var match = JsonObjectRegex.Match(json);
            if (!match.Success) return null;

            try
            {
                return JsonDocument.Parse(match.Value, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            }
            catch
            {
                return null;
            }
        }

        private static T? DeserializeLenient<T>(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return default;

            var json = content.Trim();
            if (json.StartsWith("```"))
                json = Regex.Replace(json, @"^```(?:json)?\s*|\s*```$", "",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();

            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                });
            }
            catch
            {
                // Try extracting JSON object/array
                if (typeof(T) == typeof(List<string>) && json.Contains("["))
                {
                    var start = json.IndexOf('[');
                    var end = json.LastIndexOf(']');
                    if (start >= 0 && end > start)
                    {
                        try
                        {
                            return JsonSerializer.Deserialize<T>(json[start..(end + 1)]);
                        }
                        catch { }
                    }
                }
                return default;
            }
        }

        private static string GetStringProp(JsonElement el, string name, string fallback) =>
            el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? fallback : fallback;

        private static double GetDoubleProp(JsonElement el, string name, double fallback)
        {
            if (!el.TryGetProperty(name, out var prop)) return fallback;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d)) return d;
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var d2)) return d2;
            return fallback;
        }

        private static int GetIntProp(JsonElement el, string name, int fallback)
        {
            if (!el.TryGetProperty(name, out var prop)) return fallback;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i)) return i;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var i2)) return i2;
            return fallback;
        }

        private static bool GetBoolProp(JsonElement el, string name, bool fallback)
        {
            if (!el.TryGetProperty(name, out var prop)) return fallback;
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
            if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var b)) return b;
            return fallback;
        }

        private static List<string> GetStringArray(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Array)
                return new List<string>();

            return prop.EnumerateArray()
                .Select(x => x.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
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
                    _httpClient?.Dispose();
                    _rateLimiter?.Dispose();
                    _context?.Dispose();
                }
                _disposed = true;
            }
        }
        private async Task SaveResponseAnalysisToDbAsync(ResponseAnalysisResult result)
        {
            try
            {
                // Detach any tracked entities to avoid conflicts
                var trackedEntities = _context.ChangeTracker.Entries<Model.ResponseAnalysis>()
                    .Where(e => e.Entity.ResponseId == result.ResponseId && e.Entity.QuestionId == result.QuestionId)
                    .ToList();

                foreach (var tracked in trackedEntities)
                {
                    tracked.State = EntityState.Detached;
                }

                // Check if already exists
                var existing = await _context.ResponseAnalyses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ra => ra.ResponseId == result.ResponseId && ra.QuestionId == result.QuestionId);

                if (existing != null)
                {
                    // Update existing
                    existing.AnonymizedText = result.AnonymizedResponseText;
                    existing.SentimentLabel = result.SentimentAnalysis?.Sentiment ?? "Neutral";
                    existing.SentimentConfidence = result.SentimentAnalysis?.ConfidenceScore ?? 0;
                    existing.PositiveScore = result.SentimentAnalysis?.PositiveScore ?? 0;
                    existing.NegativeScore = result.SentimentAnalysis?.NegativeScore ?? 0;
                    existing.NeutralScore = result.SentimentAnalysis?.NeutralScore ?? 0;
                    existing.RiskLevel = result.RiskAssessment?.RiskLevel.ToString() ?? "Low";
                    existing.RiskScore = result.RiskAssessment?.RiskScore ?? 0;
                    existing.RequiresImmediateAttention = result.RiskAssessment?.RequiresImmediateAttention ?? false;
                    existing.RecommendedAction = result.RiskAssessment?.RecommendedAction ?? "";
                    existing.RiskIndicatorsJson = JsonSerializer.Serialize(result.RiskAssessment?.RiskIndicators ?? new List<string>());
                    existing.ProtectiveFactorsJson = JsonSerializer.Serialize(result.RiskAssessment?.ProtectiveFactors ?? new List<string>());
                    existing.KeyPhrasesJson = JsonSerializer.Serialize(result.KeyPhrases?.KeyPhrases ?? new List<string>());
                    existing.WorkplaceFactorsJson = JsonSerializer.Serialize(result.KeyPhrases?.WorkplaceFactors ?? new List<string>());
                    existing.EmotionalIndicatorsJson = JsonSerializer.Serialize(result.KeyPhrases?.EmotionalIndicators ?? new List<string>());
                    existing.InsightsJson = JsonSerializer.Serialize(result.Insights);
                    existing.AnalyzedAt = DateTime.UtcNow;

                    _context.ResponseAnalyses.Attach(existing);
                    _context.Entry(existing).State = EntityState.Modified;
                }
                else
                {
                    // Create new
                    var entity = new Model.ResponseAnalysis
                    {
                        ResponseId = result.ResponseId,
                        QuestionId = result.QuestionId,
                        QuestionText = result.QuestionText,
                        AnonymizedText = result.AnonymizedResponseText,
                        SentimentLabel = result.SentimentAnalysis?.Sentiment ?? "Neutral",
                        SentimentConfidence = result.SentimentAnalysis?.ConfidenceScore ?? 0,
                        PositiveScore = result.SentimentAnalysis?.PositiveScore ?? 0,
                        NegativeScore = result.SentimentAnalysis?.NegativeScore ?? 0,
                        NeutralScore = result.SentimentAnalysis?.NeutralScore ?? 0,
                        RiskLevel = result.RiskAssessment?.RiskLevel.ToString() ?? "Low",
                        RiskScore = result.RiskAssessment?.RiskScore ?? 0,
                        RequiresImmediateAttention = result.RiskAssessment?.RequiresImmediateAttention ?? false,
                        RecommendedAction = result.RiskAssessment?.RecommendedAction ?? "",
                        RiskIndicatorsJson = JsonSerializer.Serialize(result.RiskAssessment?.RiskIndicators ?? new List<string>()),
                        ProtectiveFactorsJson = JsonSerializer.Serialize(result.RiskAssessment?.ProtectiveFactors ?? new List<string>()),
                        KeyPhrasesJson = JsonSerializer.Serialize(result.KeyPhrases?.KeyPhrases ?? new List<string>()),
                        WorkplaceFactorsJson = JsonSerializer.Serialize(result.KeyPhrases?.WorkplaceFactors ?? new List<string>()),
                        EmotionalIndicatorsJson = JsonSerializer.Serialize(result.KeyPhrases?.EmotionalIndicators ?? new List<string>()),
                        InsightsJson = JsonSerializer.Serialize(result.Insights),
                        AnalyzedAt = DateTime.UtcNow
                    };

                    _context.ResponseAnalyses.Add(entity);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving analysis to DB for ResponseId: {ResponseId}, QuestionId: {QuestionId}", result.ResponseId, result.QuestionId);
            }
        }

        private async Task<ResponseAnalysisResult?> LoadResponseAnalysisFromDbAsync(int responseId, int questionId)
        {
            try
            {
                var entity = await _context.ResponseAnalyses
     .AsNoTracking()
     .FirstOrDefaultAsync(ra => ra.ResponseId == responseId && ra.QuestionId == questionId);

                if (entity == null) return null;

                return new ResponseAnalysisResult
                {
                    ResponseId = entity.ResponseId,
                    QuestionId = entity.QuestionId,
                    QuestionText = entity.QuestionText,
                    AnonymizedResponseText = entity.AnonymizedText,
                    SentimentAnalysis = new SentimentAnalysisResult
                    {
                        Sentiment = entity.SentimentLabel,
                        ConfidenceScore = entity.SentimentConfidence,
                        PositiveScore = entity.PositiveScore,
                        NegativeScore = entity.NegativeScore,
                        NeutralScore = entity.NeutralScore,
                        AnalyzedAt = entity.AnalyzedAt
                    },
                    KeyPhrases = new KeyPhrasesResult
                    {
                        KeyPhrases = JsonSerializer.Deserialize<List<string>>(entity.KeyPhrasesJson) ?? new List<string>(),
                        WorkplaceFactors = JsonSerializer.Deserialize<List<string>>(entity.WorkplaceFactorsJson) ?? new List<string>(),
                        EmotionalIndicators = JsonSerializer.Deserialize<List<string>>(entity.EmotionalIndicatorsJson) ?? new List<string>(),
                        ExtractedAt = entity.AnalyzedAt
                    },
                    RiskAssessment = new MentalHealthRiskAssessment
                    {
                        RiskLevel = Enum.TryParse<RiskLevel>(entity.RiskLevel, out var rl) ? rl : RiskLevel.Low,
                        RiskScore = entity.RiskScore,
                        RequiresImmediateAttention = entity.RequiresImmediateAttention,
                        RecommendedAction = entity.RecommendedAction,
                        RiskIndicators = JsonSerializer.Deserialize<List<string>>(entity.RiskIndicatorsJson) ?? new List<string>(),
                        ProtectiveFactors = JsonSerializer.Deserialize<List<string>>(entity.ProtectiveFactorsJson) ?? new List<string>(),
                        AssessedAt = entity.AnalyzedAt
                    },
                    Insights = JsonSerializer.Deserialize<List<WorkplaceInsight>>(entity.InsightsJson) ?? new List<WorkplaceInsight>(),
                    AnalyzedAt = entity.AnalyzedAt,
                    IsAnalysisComplete = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading analysis from DB for ResponseId: {ResponseId}, QuestionId: {QuestionId}", responseId, questionId);
                return null;
            }
        }
        private async Task SaveSnapshotToDbAsync(QuestionnaireAnalysisOverview overview)
        {
            try
            {
                var existing = await _context.QuestionnaireAnalysisSnapshots
                    .FirstOrDefaultAsync(s => s.QuestionnaireId == overview.QuestionnaireId);

                if (existing != null)
                {
                    existing.TotalResponses = overview.TotalResponses;
                    existing.AnalyzedResponses = overview.AnalyzedResponses;
                    existing.OverallPositiveSentiment = overview.OverallPositiveSentiment;
                    existing.OverallNegativeSentiment = overview.OverallNegativeSentiment;
                    existing.OverallNeutralSentiment = overview.OverallNeutralSentiment;
                    existing.LowRiskCount = overview.LowRiskResponses;
                    existing.ModerateRiskCount = overview.ModerateRiskResponses;
                    existing.HighRiskCount = overview.HighRiskResponses;
                    existing.CriticalRiskCount = overview.CriticalRiskResponses;
                    existing.ExecutiveSummary = overview.ExecutiveSummary;
                    existing.TopWorkplaceIssuesJson = JsonSerializer.Serialize(overview.TopWorkplaceIssues);
                    existing.MostCommonKeyPhrasesJson = JsonSerializer.Serialize(overview.MostCommonKeyPhrases);
                    existing.GeneratedAt = DateTime.UtcNow;

                    _context.QuestionnaireAnalysisSnapshots.Update(existing);
                }
                else
                {
                    var entity = new Model.QuestionnaireAnalysisSnapshot
                    {
                        QuestionnaireId = overview.QuestionnaireId,
                        TotalResponses = overview.TotalResponses,
                        AnalyzedResponses = overview.AnalyzedResponses,
                        OverallPositiveSentiment = overview.OverallPositiveSentiment,
                        OverallNegativeSentiment = overview.OverallNegativeSentiment,
                        OverallNeutralSentiment = overview.OverallNeutralSentiment,
                        LowRiskCount = overview.LowRiskResponses,
                        ModerateRiskCount = overview.ModerateRiskResponses,
                        HighRiskCount = overview.HighRiskResponses,
                        CriticalRiskCount = overview.CriticalRiskResponses,
                        ExecutiveSummary = overview.ExecutiveSummary,
                        TopWorkplaceIssuesJson = JsonSerializer.Serialize(overview.TopWorkplaceIssues),
                        MostCommonKeyPhrasesJson = JsonSerializer.Serialize(overview.MostCommonKeyPhrases),
                        GeneratedAt = DateTime.UtcNow
                    };

                    _context.QuestionnaireAnalysisSnapshots.Add(entity);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Saved snapshot for QuestionnaireId: {QuestionnaireId}", overview.QuestionnaireId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving snapshot for QuestionnaireId: {QuestionnaireId}", overview.QuestionnaireId);
            }
        }
        private async Task<string?> BuildAnalysisTextAsync(ResponseDetail detail)
        {
            try
            {
                var questionText = detail.Question?.Text ?? "Unknown question";
                var questionType = detail.QuestionType;

                // For text-based questions, use TextResponse directly
                if (questionType == QuestionType.Text || questionType == QuestionType.Open_ended)
                {
                    return !string.IsNullOrWhiteSpace(detail.TextResponse) ? detail.TextResponse : null;
                }

                // For slider, use the numeric value with context
                if (questionType == QuestionType.Slider)
                {
                    if (!string.IsNullOrWhiteSpace(detail.TextResponse))
                    {
                        return $"Question: {questionText}\nAnswer: {detail.TextResponse}";
                    }
                    return null;
                }

                // For Rating, use TextResponse if available
                if (questionType == QuestionType.Rating)
                {
                    if (!string.IsNullOrWhiteSpace(detail.TextResponse))
                    {
                        return $"Rating Question: {questionText}\nRating given: {detail.TextResponse}";
                    }
                    return null;
                }

                // For TrueFalse, get the selected answer
                if (questionType == QuestionType.TrueFalse)
                {
                    var selectedAnswers = await GetSelectedAnswerTextsAsync(detail);
                    if (selectedAnswers.Any())
                    {
                        return $"True/False Question: {questionText}\nAnswer: {selectedAnswers.First()}";
                    }
                    return null;
                }

                // For Multiple Choice (single selection)
                if (questionType == QuestionType.Multiple_choice)
                {
                    var selectedAnswers = await GetSelectedAnswerTextsAsync(detail);
                    var otherText = !string.IsNullOrWhiteSpace(detail.TextResponse) ? $"\nAdditional comment: {detail.TextResponse}" : "";
                    if (selectedAnswers.Any())
                    {
                        return $"Multiple Choice Question: {questionText}\nSelected: {string.Join(", ", selectedAnswers)}{otherText}";
                    }
                    return null;
                }

                // For CheckBox (multiple selection)
                if (questionType == QuestionType.CheckBox)
                {
                    var selectedAnswers = await GetSelectedAnswerTextsAsync(detail);
                    var otherText = !string.IsNullOrWhiteSpace(detail.TextResponse) ? $"\nAdditional comment: {detail.TextResponse}" : "";
                    if (selectedAnswers.Any())
                    {
                        return $"Multiple Selection Question: {questionText}\nSelected Options: {string.Join(", ", selectedAnswers)}{otherText}\nAnalyze these selected workplace factors for mental health implications and patterns.";
                    }
                    return null;
                }

                // For Likert scale
                if (questionType == QuestionType.Likert)
                {
                    var selectedAnswers = await GetSelectedAnswerTextsAsync(detail);
                    if (selectedAnswers.Any())
                    {
                        return $"Likert Scale Question: {questionText}\nResponse: {string.Join(", ", selectedAnswers)}";
                    }
                    return null;
                }

                // For Matrix
                if (questionType == QuestionType.Matrix)
                {
                    var selectedAnswers = await GetSelectedAnswerTextsAsync(detail);
                    if (selectedAnswers.Any())
                    {
                        return $"Matrix Question: {questionText}\nResponses: {string.Join("; ", selectedAnswers)}";
                    }
                    return null;
                }

                // For Ranking
                if (questionType == QuestionType.Ranking)
                {
                    var selectedAnswers = await GetSelectedAnswerTextsAsync(detail);
                    if (selectedAnswers.Any())
                    {
                        return $"Ranking Question: {questionText}\nRanked order: {string.Join(" > ", selectedAnswers)}";
                    }
                    return null;
                }

                // For Demographic
                if (questionType == QuestionType.Demographic)
                {
                    if (!string.IsNullOrWhiteSpace(detail.TextResponse))
                    {
                        return $"Demographic Question: {questionText}\nAnswer: {detail.TextResponse}";
                    }
                    var selectedAnswers = await GetSelectedAnswerTextsAsync(detail);
                    if (selectedAnswers.Any())
                    {
                        return $"Demographic Question: {questionText}\nAnswer: {string.Join(", ", selectedAnswers)}";
                    }
                    return null;
                }

                // For Image-based questions
                if (questionType == QuestionType.Image)
                {
                    var selectedAnswers = await GetSelectedAnswerTextsAsync(detail);
                    if (selectedAnswers.Any())
                    {
                        return $"Image Selection Question: {questionText}\nSelected: {string.Join(", ", selectedAnswers)}";
                    }
                    return null;
                }

                // Fallback — use TextResponse if available
                if (!string.IsNullOrWhiteSpace(detail.TextResponse))
                {
                    return detail.TextResponse;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building analysis text for ResponseDetail {DetailId}", detail.Id);
                return null;
            }
        }

        private async Task<List<string>> GetSelectedAnswerTextsAsync(ResponseDetail detail)
        {
            try
            {
                if (detail.ResponseAnswers == null || !detail.ResponseAnswers.Any())
                {
                    // Load from DB if not included
                    var answerIds = await _context.Set<ResponseAnswer>()
                        .Where(ra => ra.ResponseDetailId == detail.Id)
                        .Select(ra => ra.AnswerId)
                        .ToListAsync();

                    if (!answerIds.Any()) return new List<string>();

                    var answerTexts = await _context.Set<Answer>()
                        .Where(a => answerIds.Contains(a.Id))
                        .Select(a => a.Text ?? "")
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToListAsync();

                    return answerTexts;
                }

                // Use already-loaded ResponseAnswers
                var ids = detail.ResponseAnswers.Select(ra => ra.AnswerId).ToList();
                var texts = await _context.Set<Answer>()
                    .Where(a => ids.Contains(a.Id))
                    .Select(a => a.Text ?? "")
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToListAsync();

                return texts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting selected answers for ResponseDetail {DetailId}", detail.Id);
                return new List<string>();
            }
        }

        private async Task<List<ResponseAnalysisResult>> AnalyzeAllResponsesInOneCallAsync(
    List<(int ResponseId, int QuestionId, string QuestionText, string AnswerText)> qaItems)
        {
            var results = new List<ResponseAnalysisResult>();
            if (!qaItems.Any()) return results;

            try
            {
                var qaBlock = new StringBuilder();
                for (int i = 0; i < qaItems.Count; i++)
                {
                    qaBlock.AppendLine($"[Response {i + 1}]");
                    qaBlock.AppendLine($"Question: {qaItems[i].QuestionText}");
                    qaBlock.AppendLine($"Answer: {qaItems[i].AnswerText}");
                    qaBlock.AppendLine();
                }

                var systemPrompt = @"You are a workplace mental health professional. Always respond with a single valid JSON array only. No markdown, no code fences, no explanations.";

                var userPrompt = $@"Analyze each of the following survey responses individually.

For EACH response, provide:
1. Sentiment (Positive, Negative, Neutral, Mixed)
2. Sentiment confidence scores (positive, negative, neutral — each 0.0 to 1.0, must sum to ~1.0)
3. Risk Level (Low, Moderate, High, Critical)
4. Risk Score (0.0 to 1.0)
5. Requires Immediate Attention (true/false)
6. Recommended Action
7. Risk Indicators (list of concerns)
8. Protective Factors (list of positives)
9. Key Phrases
10. Workplace Factors
11. Emotional Indicators
12. Workplace Insights — each with category, issue, recommended intervention, priority (1-5), affected areas

Survey Responses:
{qaBlock}

Return a JSON array with one object per response in the same order:
[
  {{
    ""responseIndex"": 0,
    ""sentiment"": ""Neutral"",
    ""positiveScore"": 0.05,
    ""negativeScore"": 0.05,
    ""neutralScore"": 0.90,
    ""riskLevel"": ""Low"",
    ""riskScore"": 0.1,
    ""requiresImmediateAttention"": false,
    ""recommendedAction"": ""specific action"",
    ""riskIndicators"": [""indicator1""],
    ""protectiveFactors"": [""factor1""],
    ""keyPhrases"": [""phrase1""],
    ""workplaceFactors"": [""factor1""],
    ""emotionalIndicators"": [""indicator1""],
    ""insights"": [
      {{
        ""category"": ""Category Name"",
        ""issue"": ""specific issue"",
        ""recommendedIntervention"": ""specific intervention"",
        ""priority"": 3,
        ""affectedAreas"": [""area1""]
      }}
    ]
  }}
]

Important:
- Analyze EACH response individually based on its question context
- Return exactly one object per response in the same order
- Respond with ONLY the JSON array";

                _logger.LogInformation("Sending combined analysis request for {Count} responses to Claude API", qaItems.Count);

                var content = await SendClaudeRequestAsync(systemPrompt, userPrompt, 0.3f, 8000);

                _logger.LogInformation("Received combined analysis response from Claude API");

                var parsedResults = ParseCombinedAnalysisResponse(content, qaItems);
                results.AddRange(parsedResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in combined analysis call. Falling back to individual analysis.");

                foreach (var item in qaItems)
                {
                    try
                    {
                        var request = new AnalysisRequest
                        {
                            ResponseId = item.ResponseId,
                            QuestionId = item.QuestionId,
                            ResponseText = item.AnswerText,
                            QuestionText = item.QuestionText
                        };
                        results.Add(await AnalyzeCompleteResponseAsync(request));
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Fallback analysis failed for QuestionId: {QuestionId}", item.QuestionId);
                    }
                }
            }

            return results;
        }

        private List<ResponseAnalysisResult> ParseCombinedAnalysisResponse(
            string content,
            List<(int ResponseId, int QuestionId, string QuestionText, string AnswerText)> qaItems)
        {
            var results = new List<ResponseAnalysisResult>();

            try
            {
                var json = content?.Trim() ?? "";
                if (json.StartsWith("```"))
                    json = Regex.Replace(json, @"^```(?:json)?\s*|\s*```$", "",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();

                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

                var array = doc.RootElement;
                if (array.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Combined analysis response is not a JSON array");
                    return results;
                }

                int index = 0;
                foreach (var item in array.EnumerateArray())
                {
                    if (index >= qaItems.Count) break;
                    var qa = qaItems[index];

                    var result = new ResponseAnalysisResult
                    {
                        ResponseId = qa.ResponseId,
                        QuestionId = qa.QuestionId,
                        QuestionText = qa.QuestionText,
                        ResponseText = qa.AnswerText,
                        AnonymizedResponseText = qa.AnswerText,
                        AnalyzedAt = DateTime.UtcNow,
                        IsAnalysisComplete = true
                    };

                    // Sentiment
                    result.SentimentAnalysis = new SentimentAnalysisResult
                    {
                        Sentiment = GetStringProp(item, "sentiment", "Neutral"),
                        PositiveScore = GetDoubleProp(item, "positiveScore", 0),
                        NegativeScore = GetDoubleProp(item, "negativeScore", 0),
                        NeutralScore = GetDoubleProp(item, "neutralScore", 0),
                        ConfidenceScore = Math.Max(GetDoubleProp(item, "positiveScore", 0),
                            Math.Max(GetDoubleProp(item, "negativeScore", 0), GetDoubleProp(item, "neutralScore", 0))),
                        AnalyzedAt = DateTime.UtcNow
                    };

                    // Risk
                    var riskLevelStr = GetStringProp(item, "riskLevel", "Low");
                    result.RiskAssessment = new MentalHealthRiskAssessment
                    {
                        RiskLevel = Enum.TryParse<RiskLevel>(riskLevelStr, true, out var rle) ? rle : RiskLevel.Low,
                        RiskScore = Math.Clamp(GetDoubleProp(item, "riskScore", 0), 0.0, 1.0),
                        RequiresImmediateAttention = GetBoolProp(item, "requiresImmediateAttention", false),
                        RecommendedAction = GetStringProp(item, "recommendedAction", ""),
                        RiskIndicators = GetStringArray(item, "riskIndicators"),
                        ProtectiveFactors = GetStringArray(item, "protectiveFactors"),
                        AssessedAt = DateTime.UtcNow
                    };

                    // Key Phrases
                    result.KeyPhrases = new KeyPhrasesResult
                    {
                        KeyPhrases = GetStringArray(item, "keyPhrases"),
                        WorkplaceFactors = GetStringArray(item, "workplaceFactors"),
                        EmotionalIndicators = GetStringArray(item, "emotionalIndicators"),
                        ExtractedAt = DateTime.UtcNow
                    };

                    // Insights
                    var insights = new List<WorkplaceInsight>();
                    if (item.TryGetProperty("insights", out var insArr) && insArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ins in insArr.EnumerateArray())
                        {
                            insights.Add(new WorkplaceInsight
                            {
                                Category = GetStringProp(ins, "category", "General"),
                                Issue = GetStringProp(ins, "issue", ""),
                                RecommendedIntervention = GetStringProp(ins, "recommendedIntervention", ""),
                                Priority = Math.Clamp(GetIntProp(ins, "priority", 3), 1, 5),
                                AffectedAreas = GetStringArray(ins, "affectedAreas"),
                                IdentifiedAt = DateTime.UtcNow
                            });
                        }
                    }
                    result.Insights = insights;

                    results.Add(result);
                    index++;
                }

                _logger.LogInformation("Successfully parsed {Count} results from combined analysis", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing combined analysis response. Raw (first 1000): {Raw}",
                    content?.Length > 1000 ? content[..1000] : content);
            }

            return results;
        }
        #endregion



    }


}