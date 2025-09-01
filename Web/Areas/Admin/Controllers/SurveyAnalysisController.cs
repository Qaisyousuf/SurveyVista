// Web/Areas/Admin/Controllers/SurveyAnalysisController.cs
using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;
using Services.AIViewModel;
using Services.Interaces;
using System.Text;
using System.Text.Json;

namespace Web.Areas.Admin.Controllers
{
    
    public class SurveyAnalysisController : Controller
    {
        private readonly IAiAnalysisService _aiAnalysisService;
        private readonly SurveyContext _context;
        private readonly ILogger<SurveyAnalysisController> _logger;

        public SurveyAnalysisController(
            IAiAnalysisService aiAnalysisService,
            SurveyContext context,
            ILogger<SurveyAnalysisController> logger)
        {
            _aiAnalysisService = aiAnalysisService;
            _context = context;
            _logger = logger;
        }

        #region Dashboard and Overview

        /// <summary>
        /// Main dashboard showing all questionnaires available for analysis
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var questionnaires = await _context.Questionnaires
                    .Include(q => q.Questions)
                    .Select(q => new
                    {
                        q.Id,
                        q.Title,
                        q.Description,
                        QuestionCount = q.Questions.Count,
                        ResponseCount = _context.Responses.Count(r => r.QuestionnaireId == q.Id),
                        TextResponseCount = _context.Responses
                            .Where(r => r.QuestionnaireId == q.Id)
                            .SelectMany(r => r.ResponseDetails)
                            .Count(rd => !string.IsNullOrEmpty(rd.TextResponse)),
                        LastResponse = _context.Responses
                            .Where(r => r.QuestionnaireId == q.Id)
                            .OrderByDescending(r => r.SubmissionDate)
                            .Select(r => r.SubmissionDate)
                            .FirstOrDefault(),
                        // Add Users information for displaying participant details
                        Users = _context.Responses
                            .Where(r => r.QuestionnaireId == q.Id && !string.IsNullOrEmpty(r.UserName))
                            .OrderByDescending(r => r.SubmissionDate)
                            .Select(r => new
                            {
                                UserName = r.UserName,
                                Email = r.UserEmail
                            })
                            .Distinct()
                            .Take(5) // Show up to 5 recent participants
                            .ToList()
                    })
                    .ToListAsync();

                ViewBag.ServiceHealth = await _aiAnalysisService.GetServiceHealthStatusAsync();

                return View(questionnaires);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading survey analysis dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard. Please try again.";
                return View(new List<object>());
            }
        }

        /// <summary>
        /// Generate comprehensive analysis overview for a questionnaire
        /// </summary>
        public async Task<IActionResult> AnalyzeQuestionnaire(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires
                    .Include(q => q.Questions)
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (questionnaire == null)
                {
                    TempData["ErrorMessage"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if there are responses to analyze
                var hasResponses = await _context.Responses
                    .AnyAsync(r => r.QuestionnaireId == id);

                if (!hasResponses)
                {
                    TempData["WarningMessage"] = "No responses found for this questionnaire.";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogInformation("Starting analysis for questionnaire {QuestionnaireId}", id);

                // Generate comprehensive analysis
                var analysisOverview = await _aiAnalysisService.GenerateQuestionnaireOverviewAsync(id);

                _logger.LogInformation("Analysis completed successfully for questionnaire {QuestionnaireId}", id);

                return View(analysisOverview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing questionnaire {QuestionnaireId}: {ErrorMessage}", id, ex.Message);
                TempData["ErrorMessage"] = $"Error analyzing questionnaire: {ex.Message}. Please check the logs for more details.";
                return RedirectToAction(nameof(Index));
            }
        }

        #endregion

        #region High-Risk Response Management

        /// <summary>
        /// Identify and display high-risk responses requiring immediate attention
        /// </summary>
        public async Task<IActionResult> HighRiskResponses(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (questionnaire == null)
                {
                    TempData["ErrorMessage"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                var highRiskResponses = await _aiAnalysisService.IdentifyHighRiskResponsesAsync(id);

                ViewBag.QuestionnaireName = questionnaire.Title;
                ViewBag.QuestionnaireId = id;

                return View(highRiskResponses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error identifying high-risk responses for questionnaire {QuestionnaireId}", id);
                TempData["ErrorMessage"] = "Error identifying high-risk responses. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// View detailed analysis of a specific high-risk response
        /// </summary>
        public async Task<IActionResult> ViewHighRiskResponse(int questionnaireId, int responseId)
        {
            try
            {
                var response = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                    .FirstOrDefaultAsync(r => r.Id == responseId && r.QuestionnaireId == questionnaireId);

                if (response == null)
                {
                    TempData["ErrorMessage"] = "Response not found.";
                    return RedirectToAction(nameof(HighRiskResponses), new { id = questionnaireId });
                }

                // Get AI analysis for each text response
                var analysisResults = new List<ResponseAnalysisResult>();

                foreach (var detail in response.ResponseDetails.Where(rd => !string.IsNullOrWhiteSpace(rd.TextResponse)))
                {
                    var analysisRequest = new AnalysisRequest
                    {
                        ResponseId = response.Id,
                        QuestionId = detail.QuestionId,
                        ResponseText = detail.TextResponse,
                        QuestionText = detail.Question?.Text ?? ""
                    };

                    var analysis = await _aiAnalysisService.AnalyzeCompleteResponseAsync(analysisRequest);
                    analysisResults.Add(analysis);
                }

                ViewBag.Response = response;
                return View(analysisResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing high-risk response {ResponseId}", responseId);
                TempData["ErrorMessage"] = "Error loading response details. Please try again.";
                return RedirectToAction(nameof(HighRiskResponses), new { id = questionnaireId });
            }
        }

        #endregion

        #region Individual Response Analysis

        /// <summary>
        /// Analyze a single response in detail
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AnalyzeResponse(int responseId, int questionId, string responseText, string questionText)
        {
            try
            {
                var analysisRequest = new AnalysisRequest
                {
                    ResponseId = responseId,
                    QuestionId = questionId,
                    ResponseText = responseText,
                    QuestionText = questionText
                };

                var isValid = await _aiAnalysisService.ValidateAnalysisRequestAsync(analysisRequest);
                if (!isValid)
                {
                    return Json(new { success = false, message = "Invalid analysis request." });
                }

                var analysis = await _aiAnalysisService.AnalyzeCompleteResponseAsync(analysisRequest);

                return Json(new
                {
                    success = true,
                    analysis = new
                    {
                        sentiment = analysis.SentimentAnalysis,
                        keyPhrases = analysis.KeyPhrases?.KeyPhrases ?? new List<string>(),
                        riskLevel = analysis.RiskAssessment?.RiskLevel.ToString(),
                        riskScore = analysis.RiskAssessment?.RiskScore ?? 0,
                        requiresAttention = analysis.RiskAssessment?.RequiresImmediateAttention ?? false,
                        recommendedAction = analysis.RiskAssessment?.RecommendedAction ?? "",
                        insights = analysis.Insights.Select(i => new {
                            category = i.Category,
                            issue = i.Issue,
                            intervention = i.RecommendedIntervention,
                            priority = i.Priority
                        })
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing individual response {ResponseId}", responseId);
                return Json(new { success = false, message = "Error analyzing response. Please try again." });
            }
        }

        #endregion

        #region Batch Analysis

        /// <summary>
        /// Process batch analysis for all responses in a questionnaire
        /// </summary>
        public async Task<IActionResult> BatchAnalyze(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (questionnaire == null)
                {
                    TempData["ErrorMessage"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Get all text responses for the questionnaire
                var responses = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                    .Where(r => r.QuestionnaireId == id)
                    .ToListAsync();

                var analysisRequests = new List<AnalysisRequest>();

                foreach (var response in responses)
                {
                    foreach (var detail in response.ResponseDetails.Where(rd => !string.IsNullOrWhiteSpace(rd.TextResponse)))
                    {
                        analysisRequests.Add(new AnalysisRequest
                        {
                            ResponseId = response.Id,
                            QuestionId = detail.QuestionId,
                            ResponseText = detail.TextResponse,
                            QuestionText = detail.Question?.Text ?? ""
                        });
                    }
                }

                if (!analysisRequests.Any())
                {
                    TempData["WarningMessage"] = "No text responses found to analyze.";
                    return RedirectToAction(nameof(AnalyzeQuestionnaire), new { id });
                }

                // Process batch analysis (this might take a while)
                ViewBag.QuestionnaireName = questionnaire.Title;
                ViewBag.QuestionnaireId = id;
                ViewBag.TotalRequests = analysisRequests.Count;

                return View("BatchAnalysisProgress");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting batch analysis for questionnaire {QuestionnaireId}", id);
                TempData["ErrorMessage"] = "Error starting batch analysis. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// AJAX endpoint for batch analysis progress
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProcessBatchAnalysis(int questionnaireId)
        {
            try
            {
                var responses = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                    .Where(r => r.QuestionnaireId == questionnaireId)
                    .ToListAsync();

                var analysisRequests = new List<AnalysisRequest>();

                foreach (var response in responses)
                {
                    foreach (var detail in response.ResponseDetails.Where(rd => !string.IsNullOrWhiteSpace(rd.TextResponse)))
                    {
                        analysisRequests.Add(new AnalysisRequest
                        {
                            ResponseId = response.Id,
                            QuestionId = detail.QuestionId,
                            ResponseText = detail.TextResponse,
                            QuestionText = detail.Question?.Text ?? ""
                        });
                    }
                }

                var results = await _aiAnalysisService.BatchAnalyzeResponsesAsync(analysisRequests);

                return Json(new
                {
                    success = true,
                    processedCount = results.Count,
                    highRiskCount = results.Count(r => r.RiskAssessment?.RiskLevel >= RiskLevel.High),
                    message = $"Successfully analyzed {results.Count} responses."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch analysis for questionnaire {QuestionnaireId}", questionnaireId);
                return Json(new
                {
                    success = false,
                    message = "Error processing batch analysis. Please try again."
                });
            }
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generate detailed analysis report for management
        /// </summary>
        public async Task<IActionResult> GenerateReport(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (questionnaire == null)
                {
                    TempData["ErrorMessage"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                var report = await _aiAnalysisService.GenerateDetailedAnalysisReportAsync(id);

                ViewBag.QuestionnaireName = questionnaire.Title;
                ViewBag.QuestionnaireId = id;
                ViewBag.Report = report;
                ViewBag.GeneratedDate = DateTime.Now;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report for questionnaire {QuestionnaireId}", id);
                TempData["ErrorMessage"] = "Error generating report. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Download report as text file
        /// </summary>
        public async Task<IActionResult> DownloadReport(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (questionnaire == null)
                {
                    TempData["ErrorMessage"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                var report = await _aiAnalysisService.GenerateDetailedAnalysisReportAsync(id);
                var bytes = Encoding.UTF8.GetBytes(report);

                var fileName = $"Mental_Health_Analysis_{questionnaire.Title}_{DateTime.Now:yyyy-MM-dd}.txt";

                return File(bytes, "text/plain", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading report for questionnaire {QuestionnaireId}", id);
                TempData["ErrorMessage"] = "Error downloading report. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Export anonymized analysis data
        /// </summary>
        public async Task<IActionResult> ExportAnalysis(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (questionnaire == null)
                {
                    TempData["ErrorMessage"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                var analysisData = await _aiAnalysisService.ExportAnonymizedAnalysisAsync(id);

                var json = System.Text.Json.JsonSerializer.Serialize(analysisData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var bytes = Encoding.UTF8.GetBytes(json);
                var fileName = $"Anonymized_Analysis_{questionnaire.Title}_{DateTime.Now:yyyy-MM-dd}.json";

                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting analysis for questionnaire {QuestionnaireId}", id);
                TempData["ErrorMessage"] = "Error exporting analysis. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        #endregion

        #region Mental Health Trends

        /// <summary>
        /// Analyze mental health trends over time periods
        /// </summary>
        public async Task<IActionResult> AnalyzeTrends(int id, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var questionnaire = await _context.Questionnaires
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (questionnaire == null)
                {
                    TempData["ErrorMessage"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Default to last 6 months if no dates provided
                var from = fromDate ?? DateTime.Now.AddMonths(-6);
                var to = toDate ?? DateTime.Now;

                var trends = await _aiAnalysisService.AnalyzeMentalHealthTrendsAsync(id, from, to);

                ViewBag.QuestionnaireName = questionnaire.Title;
                ViewBag.QuestionnaireId = id;
                ViewBag.FromDate = from;
                ViewBag.ToDate = to;

                return View(trends);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing trends for questionnaire {QuestionnaireId}", id);
                TempData["ErrorMessage"] = "Error analyzing trends. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        #endregion

        #region Service Health and Testing

        /// <summary>
        /// Check AI service health status
        /// </summary>
        public async Task<IActionResult> ServiceHealth()
        {
            try
            {
                var healthStatus = await _aiAnalysisService.GetServiceHealthStatusAsync();
                return Json(new
                {
                    success = true,
                    services = healthStatus,
                    message = "Service health check completed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking service health");
                return Json(new
                {
                    success = false,
                    error = "Unable to check service health",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Test AI analysis with sample text
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> TestAnalysis(string sampleText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sampleText))
                {
                    return Json(new { success = false, message = "Please provide sample text." });
                }

                var analysisRequest = new AnalysisRequest
                {
                    ResponseId = 0, // Test request
                    QuestionId = 0, // Test request
                    ResponseText = sampleText,
                    QuestionText = "Test question: How are you feeling about your work environment?"
                };

                var analysis = await _aiAnalysisService.AnalyzeCompleteResponseAsync(analysisRequest);

                return Json(new
                {
                    success = true,
                    sentiment = analysis.SentimentAnalysis?.Sentiment,
                    riskLevel = analysis.RiskAssessment?.RiskLevel.ToString(),
                    keyPhrases = analysis.KeyPhrases?.KeyPhrases,
                    insights = analysis.Insights.Select(i => i.Category).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test analysis");
                return Json(new
                {
                    success = false,
                    message = "Error performing test analysis. Please try again."
                });
            }
        }

        #endregion

        #region Management Dashboard

        /// <summary>
        /// Executive dashboard for mental health overview
        /// </summary>
        public async Task<IActionResult> Dashboard(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (questionnaire == null)
                {
                    TempData["ErrorMessage"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                var dashboard = await _aiAnalysisService.GenerateManagementDashboardAsync(id);

                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard for questionnaire {QuestionnaireId}", id);
                TempData["ErrorMessage"] = "Error loading dashboard. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        #endregion
    }
}