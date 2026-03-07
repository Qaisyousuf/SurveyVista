
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Services.AIViewModel;
using Services.Interaces;
using Data;
using Model;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Services.Implemnetation
{
    public class UserTrajectoryService : IUserTrajectoryService
    {
        private readonly SurveyContext _context;
        private readonly ILogger<UserTrajectoryService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _claudeApiKey;
        private readonly string _claudeModel;

        public UserTrajectoryService(
            IConfiguration configuration,
            ILogger<UserTrajectoryService> logger,
            SurveyContext context)
        {
            _logger = logger;
            _context = context;

            // Claude API configuration
            _claudeApiKey = configuration["Claude:ApiKey"]
                ?? throw new ArgumentNullException("Claude:ApiKey is missing from configuration");
            _claudeModel = configuration["Claude:Model"] ?? "claude-sonnet-4-20250514";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _claudeApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            _logger.LogInformation("UserTrajectoryService initialized with Claude API (Model: {Model})", _claudeModel);
        }

        // ═══════════════════════════════════════════
        // PUBLIC METHODS
        // ═══════════════════════════════════════════

        public async Task<UserTrajectoryAnalysis> GetOrAnalyzeTrajectoryAsync(string userEmail)
        {
            try
            {
                // 1. Count current responses
                var currentResponses = await GetUserResponses(userEmail);
                var currentCount = currentResponses.Count;

                if (currentCount == 0)
                    return CreateEmptyResult();

                // 2. Check cache
                var cache = await _context.UserTrajectoryCaches
                    .FirstOrDefaultAsync(c => c.UserEmail == userEmail);

                if (cache != null && cache.AnalyzedResponseCount == currentCount)
                {
                    // Cache is fresh — return it
                    _logger.LogInformation("Returning cached trajectory for {Email} ({Count} responses)", userEmail, currentCount);
                    return DeserializeResult(cache.TrajectoryJson);
                }

                // 3. Need to analyze
                UserTrajectoryAnalysis result;

                if (cache == null)
                {
                    // First-time analysis — send ALL responses
                    _logger.LogInformation("First trajectory analysis for {Email} ({Count} responses)", userEmail, currentCount);
                    var responseText = BuildFullResponseText(currentResponses);
                    result = await CallClaudeFullAnalysis(responseText, currentCount);
                }
                else
                {
                    // Incremental — send only NEW responses + previous summary
                    var newResponses = currentResponses
                        .Where(r => r.SubmissionDate > cache.LastResponseDate)
                        .OrderBy(r => r.SubmissionDate)
                        .ToList();

                    _logger.LogInformation("Incremental trajectory for {Email} ({New} new of {Total} total)",
                        userEmail, newResponses.Count, currentCount);

                    var newResponseText = BuildFullResponseText(newResponses);
                    result = await CallClaudeIncrementalAnalysis(
                        newResponseText, newResponses.Count,
                        cache.PreviousSummary ?? "", currentCount);

                    result.IsIncremental = true;
                }

                result.TotalResponsesAnalyzed = currentCount;
                result.AnalyzedAt = DateTime.UtcNow;

                // 4. Save to cache
                var latestDate = currentResponses.Max(r => r.SubmissionDate);
                var json = SerializeResult(result);
                var summary = BuildSummaryForCache(result);

                if (cache == null)
                {
                    _context.UserTrajectoryCaches.Add(new UserTrajectoryCache
                    {
                        UserEmail = userEmail,
                        AnalyzedResponseCount = currentCount,
                        LastResponseDate = latestDate,
                        TrajectoryJson = json,
                        PreviousSummary = summary,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    cache.AnalyzedResponseCount = currentCount;
                    cache.LastResponseDate = latestDate;
                    cache.TrajectoryJson = json;
                    cache.PreviousSummary = summary;
                    cache.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing trajectory for {Email}", userEmail);
                throw;
            }
        }

        public async Task<UserTrajectoryAnalysis> ForceReanalyzeTrajectoryAsync(string userEmail)
        {
            var cache = await _context.UserTrajectoryCaches
                .FirstOrDefaultAsync(c => c.UserEmail == userEmail);

            if (cache != null)
            {
                _context.UserTrajectoryCaches.Remove(cache);
                await _context.SaveChangesAsync();
            }

            return await GetOrAnalyzeTrajectoryAsync(userEmail);
        }

        public async Task<(bool HasCache, bool IsStale, int CachedCount, int CurrentCount)> CheckCacheStatusAsync(string userEmail)
        {
            var currentCount = await _context.Responses.CountAsync(r => r.UserEmail == userEmail);
            var cache = await _context.UserTrajectoryCaches.FirstOrDefaultAsync(c => c.UserEmail == userEmail);

            if (cache == null)
                return (false, true, 0, currentCount);

            var isStale = cache.AnalyzedResponseCount < currentCount;
            return (true, isStale, cache.AnalyzedResponseCount, currentCount);
        }

        // ═══════════════════════════════════════════
        // DATA BUILDING — Complete response data
        // ═══════════════════════════════════════════

        private async Task<List<Response>> GetUserResponses(string userEmail)
        {
            return await _context.Responses
                .Include(r => r.Questionnaire)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(q => q.Answers)
                .Include(r => r.ResponseDetails)
                    .ThenInclude(rd => rd.Question)
                        .ThenInclude(q => q.Answers)
                .Include(r => r.ResponseDetails)
                    .ThenInclude(rd => rd.ResponseAnswers)
                        .ThenInclude(ra => ra.Answer)
                .Where(r => r.UserEmail == userEmail)
                .OrderBy(r => r.SubmissionDate)
                .ToListAsync();
        }

        /// <summary>
        /// Builds a complete text representation of all responses.
        /// Includes ALL questions with types, available options, and user answers.
        /// NO personal data (name, email) is included.
        /// </summary>
        private string BuildFullResponseText(List<Response> responses)
        {
            var sb = new StringBuilder();
            int responseNum = 0;

            foreach (var response in responses)
            {
                responseNum++;
                sb.AppendLine($"--- Response {responseNum}: \"{response.Questionnaire?.Title ?? "Unknown Survey"}\" (Submitted: {response.SubmissionDate:MMMM dd, yyyy}) ---");
                sb.AppendLine();

                var questions = response.Questionnaire?.Questions?.OrderBy(q => q.Id).ToList()
                    ?? new List<Question>();

                int qNum = 0;
                foreach (var question in questions)
                {
                    qNum++;
                    sb.AppendLine($"  Q{qNum}: {question.Text} [{question.Type}]");

                    // Show available options for non-text questions
                    if (question.Answers != null && question.Answers.Any())
                    {
                        var optionTexts = question.Answers.Select(a => a.Text).ToList();
                        sb.AppendLine($"     Options: {string.Join(", ", optionTexts)}");
                    }

                    // Find the user's response detail for this question
                    var detail = response.ResponseDetails?.FirstOrDefault(d => d.QuestionId == question.Id);

                    if (detail == null)
                    {
                        sb.AppendLine($"     → (No response recorded)");
                    }
                    else
                    {
                        // Text response
                        if (!string.IsNullOrWhiteSpace(detail.TextResponse))
                        {
                            sb.AppendLine($"     → Text Answer: \"{detail.TextResponse}\"");
                        }

                        // Selected answers (checkbox, radio, multiple choice)
                        if (detail.ResponseAnswers != null && detail.ResponseAnswers.Any())
                        {
                            var selectedTexts = detail.ResponseAnswers
                                .Where(ra => ra.Answer != null)
                                .Select(ra => ra.Answer!.Text)
                                .ToList();

                            if (selectedTexts.Any())
                            {
                                sb.AppendLine($"     → Selected: {string.Join(", ", selectedTexts)}");
                            }
                        }

                        // Other/custom text
                        if (!string.IsNullOrWhiteSpace(detail.OtherText))
                        {
                            sb.AppendLine($"     → Custom Response: \"{detail.OtherText}\"");
                        }

                        // Status
                        if (detail.Status == ResponseStatus.Skipped)
                        {
                            sb.AppendLine($"     → (Skipped{(string.IsNullOrEmpty(detail.SkipReason) ? "" : $": {detail.SkipReason}")})");
                        }
                        else if (detail.Status == ResponseStatus.Shown)
                        {
                            sb.AppendLine($"     → (Shown but not answered)");
                        }
                    }

                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ═══════════════════════════════════════════
        // CLAUDE API CALLS
        // ═══════════════════════════════════════════

        private async Task<UserTrajectoryAnalysis> CallClaudeFullAnalysis(string responseText, int responseCount)
        {
            var isMultiple = responseCount > 1;
            var trajectoryInstruction = isMultiple
                ? "Analyze the TRAJECTORY of this employee's mental health over time. Compare responses chronologically. Determine if their wellbeing is improving, stable, declining, or fluctuating."
                : "This is a SINGLE initial assessment. Analyze the employee's current mental health state as a baseline evaluation. Set trajectoryDirection to 'Initial'.";

            var systemPrompt = "You are a senior workplace mental health consultant. You analyze anonymized employee survey responses to assess mental health trajectories over time. You NEVER receive personal data — only survey questions, answer options, and the employee's responses. Always respond with a SINGLE valid JSON object. No markdown, no code fences, no explanations outside the JSON.";

            var userPrompt = $@"{trajectoryInstruction}

Employee Survey Data ({responseCount} response{(responseCount > 1 ? "s" : "")}):

{responseText}

Respond with this exact JSON structure:
{{
    ""trajectoryDirection"": ""Improving|Stable|Declining|Fluctuating|Initial"",
    ""trajectoryScore"": 0,
    ""scoreChange"": 0,
    ""overallRiskLevel"": ""Low|Moderate|High|Critical"",
    ""executiveSummary"": ""2-3 sentence overview"",
    ""detailedAnalysis"": ""Detailed paragraph with specific observations"",
    ""responseSnapshots"": [
        {{
            ""responseDate"": ""MMMM dd, yyyy"",
            ""questionnaireName"": ""name"",
            ""wellnessScore"": 0,
            ""riskLevel"": ""Low|Moderate|High|Critical"",
            ""sentimentLabel"": ""Positive|Negative|Mixed|Neutral"",
            ""keyThemes"": [""theme1"", ""theme2""],
            ""briefSummary"": ""One sentence""
        }}
    ],
    ""patternInsights"": [
        {{
            ""pattern"": ""Description of cross-response pattern"",
            ""severity"": ""High|Medium|Low"",
            ""firstSeen"": ""date"",
            ""stillPresent"": true
        }}
    ],
    ""strengthFactors"": [
        {{ ""factor"": ""Positive observation"" }}
    ],
    ""concernFactors"": [
        {{ ""concern"": ""Concern description"", ""urgency"": ""Immediate|Monitor|Low"" }}
    ],
    ""recommendations"": [
        {{ ""action"": ""Specific action"", ""priority"": ""Urgent|High|Normal"", ""category"": ""Workplace|Personal|Professional Support"" }}
    ],
    ""timelineNarrative"": ""A professional narrative describing the employee's mental health journey suitable for case reports""
}}

IMPORTANT: trajectoryScore and wellnessScore must be integers 0-100 where 100 is excellent mental health. scoreChange is the difference between first and last response scores (positive = improvement). Provide at least 2 patternInsights, 2 strengthFactors, 2 concernFactors, and 3 recommendations.";

            return await CallClaudeApi(systemPrompt, userPrompt);
        }

        private async Task<UserTrajectoryAnalysis> CallClaudeIncrementalAnalysis(
            string newResponseText, int newCount, string previousSummary, int totalCount)
        {
            var systemPrompt = "You are a senior workplace mental health consultant. You are updating an existing employee trajectory analysis with new survey data. You NEVER receive personal data — only survey questions, answer options, and responses. Always respond with a SINGLE valid JSON object. No markdown, no code fences.";

            var userPrompt = $@"PREVIOUS ANALYSIS SUMMARY (based on {totalCount - newCount} earlier responses):
{previousSummary}

NEW SURVEY DATA ({newCount} new response{(newCount > 1 ? "s" : "")} — total is now {totalCount}):

{newResponseText}

Update the trajectory analysis incorporating this new data with the existing history. The trajectoryScore and scoreChange should reflect the FULL trajectory (all {totalCount} responses), not just the new ones.

Respond with this exact JSON structure:
{{
    ""trajectoryDirection"": ""Improving|Stable|Declining|Fluctuating"",
    ""trajectoryScore"": 0,
    ""scoreChange"": 0,
    ""overallRiskLevel"": ""Low|Moderate|High|Critical"",
    ""executiveSummary"": ""2-3 sentence overview of full trajectory"",
    ""detailedAnalysis"": ""Detailed paragraph incorporating both old and new data"",
    ""responseSnapshots"": [
        {{
            ""responseDate"": ""date"",
            ""questionnaireName"": ""name"",
            ""wellnessScore"": 0,
            ""riskLevel"": ""Low|Moderate|High|Critical"",
            ""sentimentLabel"": ""Positive|Negative|Mixed|Neutral"",
            ""keyThemes"": [""theme1""],
            ""briefSummary"": ""One sentence""
        }}
    ],
    ""patternInsights"": [
        {{ ""pattern"": ""description"", ""severity"": ""High|Medium|Low"", ""firstSeen"": ""date"", ""stillPresent"": true }}
    ],
    ""strengthFactors"": [{{ ""factor"": ""observation"" }}],
    ""concernFactors"": [{{ ""concern"": ""description"", ""urgency"": ""Immediate|Monitor|Low"" }}],
    ""recommendations"": [{{ ""action"": ""action"", ""priority"": ""Urgent|High|Normal"", ""category"": ""Workplace|Personal|Professional Support"" }}],
    ""timelineNarrative"": ""Updated professional narrative for full trajectory""
}}

IMPORTANT: responseSnapshots should include entries for ALL {totalCount} responses (use the previous summary to reconstruct earlier snapshots). Scores are 0-100 integers.";

            return await CallClaudeApi(systemPrompt, userPrompt);
        }

        // ═══════════════════════════════════════════
        // CLAUDE HTTP API CALL
        // ═══════════════════════════════════════════

        private async Task<UserTrajectoryAnalysis> CallClaudeApi(string systemPrompt, string userPrompt)
        {
            var requestBody = new
            {
                model = _claudeModel,
                max_tokens = 4096,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                }
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", httpContent);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Claude API error {StatusCode}: {Error}", httpResponse.StatusCode, errorBody);
                throw new Exception($"Claude API returned {httpResponse.StatusCode}: {errorBody}");
            }

            var responseJson = await httpResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("Claude API response length: {Length} chars", responseJson.Length);

            // Extract the text content from Claude's response
            var claudeResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var contentArray = claudeResponse.GetProperty("content");
            var textContent = "";

            foreach (var block in contentArray.EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "text")
                {
                    textContent = block.GetProperty("text").GetString() ?? "";
                    break;
                }
            }

            if (string.IsNullOrEmpty(textContent))
            {
                _logger.LogWarning("Claude returned empty text content");
                return CreateFallbackResult();
            }

            // Parse the JSON from Claude's text response
            var result = DeserializeLenient<UserTrajectoryAnalysis>(textContent, out var error);

            if (result == null)
            {
                _logger.LogWarning("Failed to parse trajectory JSON from Claude: {Error}. Raw (first 1000): {Raw}",
                    error, textContent.Length > 1000 ? textContent[..1000] : textContent);
                return CreateFallbackResult();
            }

            return result;
        }

        // ═══════════════════════════════════════════
        // CACHE HELPERS
        // ═══════════════════════════════════════════

        private string BuildSummaryForCache(UserTrajectoryAnalysis result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Trajectory: {result.TrajectoryDirection} | Score: {result.TrajectoryScore}/100 | Risk: {result.OverallRiskLevel}");
            sb.AppendLine($"Summary: {result.ExecutiveSummary}");

            if (result.ResponseSnapshots.Any())
            {
                sb.AppendLine("Response History:");
                foreach (var snap in result.ResponseSnapshots)
                {
                    sb.AppendLine($"  - {snap.ResponseDate} ({snap.QuestionnaireName}): Wellness {snap.WellnessScore}/100, {snap.RiskLevel} risk, {snap.SentimentLabel} sentiment. {snap.BriefSummary}");
                }
            }

            if (result.PatternInsights.Any())
            {
                sb.AppendLine("Key Patterns: " + string.Join("; ", result.PatternInsights.Select(p => $"{p.Pattern} ({p.Severity})")));
            }

            if (result.ConcernFactors.Any())
            {
                sb.AppendLine("Concerns: " + string.Join("; ", result.ConcernFactors.Select(c => $"{c.Concern} ({c.Urgency})")));
            }

            if (result.StrengthFactors.Any())
            {
                sb.AppendLine("Strengths: " + string.Join("; ", result.StrengthFactors.Select(s => s.Factor)));
            }

            return sb.ToString();
        }

        private UserTrajectoryAnalysis CreateEmptyResult()
        {
            return new UserTrajectoryAnalysis
            {
                TrajectoryDirection = "Initial",
                TrajectoryScore = 0,
                OverallRiskLevel = "Low",
                ExecutiveSummary = "No survey responses found for this user.",
                AnalyzedAt = DateTime.UtcNow
            };
        }

        private UserTrajectoryAnalysis CreateFallbackResult()
        {
            return new UserTrajectoryAnalysis
            {
                TrajectoryDirection = "Initial",
                TrajectoryScore = 50,
                OverallRiskLevel = "Moderate",
                ExecutiveSummary = "Analysis could not be fully parsed. Please try re-analyzing.",
                DetailedAnalysis = "The AI response could not be parsed into the expected format. Please use the 'Re-analyze' option to try again.",
                AnalyzedAt = DateTime.UtcNow
            };
        }

        // ═══════════════════════════════════════════
        // JSON SERIALIZATION
        // ═══════════════════════════════════════════

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private string SerializeResult(UserTrajectoryAnalysis result)
        {
            return JsonSerializer.Serialize(result, _jsonOptions);
        }

        private UserTrajectoryAnalysis DeserializeResult(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<UserTrajectoryAnalysis>(json, _jsonOptions)
                    ?? new UserTrajectoryAnalysis();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached trajectory JSON");
                return new UserTrajectoryAnalysis
                {
                    ExecutiveSummary = "Cached data could not be loaded. Please re-analyze."
                };
            }
        }

        // ═══════════════════════════════════════════
        // JSON PARSE HELPERS
        // ═══════════════════════════════════════════

        private static readonly Regex JsonObjectRegex =
            new(@"(\{(?:[^{}]|(?<o>\{)|(?<-o>\}))*(?(o)(?!))\})",
                RegexOptions.Singleline | RegexOptions.Compiled);

        private static T? DeserializeLenient<T>(string content, out string? error)
        {
            error = null;
            var json = content?.Trim() ?? "";

            // Strip markdown fences
            if (json.StartsWith("```"))
            {
                json = Regex.Replace(json, "^```(?:json)?\\s*|\\s*```$", "",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();
            }

            // Try direct parse first
            if (json.StartsWith("{") && json.EndsWith("}"))
            {
                try { return JsonSerializer.Deserialize<T>(json, _jsonOptions); }
                catch (Exception ex) { error = ex.Message; }
            }

            // Try regex extraction
            var m = JsonObjectRegex.Match(json);
            if (m.Success)
            {
                try { return JsonSerializer.Deserialize<T>(m.Value, _jsonOptions); }
                catch (Exception ex) { error = ex.Message; }
            }

            error ??= "No valid JSON object found in response.";
            return default;
        }
    }
}