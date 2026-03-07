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
using Web.Authorization;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
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
                var responses = await _context.Responses
                    .Include(r => r.Questionnaire)
                        .ThenInclude(q => q.Questions)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .OrderByDescending(r => r.SubmissionDate)
                    .ToListAsync();

                var result = responses.Select(r => new
                {
                    ResponseId = r.Id,
                    QuestionnaireId = r.QuestionnaireId,
                    Title = r.Questionnaire?.Title ?? "Unknown",
                    Description = r.Questionnaire?.Description ?? "",
                    QuestionCount = r.Questionnaire?.Questions?.Count ?? 0,
                    AnalyzableCount = r.ResponseDetails.Count(rd =>
                        !string.IsNullOrEmpty(rd.TextResponse) ||
                        rd.ResponseAnswers.Any()),
                    TotalAnswered = r.ResponseDetails.Count,
                    SubmissionDate = r.SubmissionDate,
                    UserName = r.UserName ?? "Anonymous",
                    UserEmail = r.UserEmail ?? "No email available"
                }).ToList();

                ViewBag.ServiceHealth = await _aiAnalysisService.GetServiceHealthStatusAsync();
                return View(result);
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
        [HasPermission(Permissions.SurveyAnalysis.Analyze)]
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

                // Check if there are ANY responses (not just text)
                var hasResponses = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .AnyAsync(r => r.QuestionnaireId == id &&
                        r.ResponseDetails.Any(rd =>
                            !string.IsNullOrEmpty(rd.TextResponse) ||
                            rd.ResponseAnswers.Any()));

                if (!hasResponses)
                {
                    TempData["WarningMessage"] = "No analyzable responses found for this questionnaire.";
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogInformation("Starting analysis for questionnaire {QuestionnaireId}", id);

                var analysisOverview = await _aiAnalysisService.GenerateQuestionnaireOverviewAsync(id);

                _logger.LogInformation("Analysis completed successfully for questionnaire {QuestionnaireId}", id);

                return View(analysisOverview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing questionnaire {QuestionnaireId}: {ErrorMessage}", id, ex.Message);
                TempData["ErrorMessage"] = $"Error analyzing questionnaire: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        #endregion

        #region High-Risk Response Management

        /// <summary>
        /// Identify and display high-risk responses requiring immediate attention
        /// </summary>
        [HasPermission(Permissions.SurveyAnalysis.HighRisk)]
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
        [HasPermission(Permissions.SurveyAnalysis.HighRisk)]
        public async Task<IActionResult> ViewHighRiskResponse(int questionnaireId, int responseId)
        {
            try
            {
                var response = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                            .ThenInclude(ra => ra.Answer)
                    .FirstOrDefaultAsync(r => r.Id == responseId && r.QuestionnaireId == questionnaireId);

                if (response == null)
                {
                    TempData["ErrorMessage"] = "Response not found.";
                    return RedirectToAction(nameof(HighRiskResponses), new { id = questionnaireId });
                }

                var analysisResults = new List<ResponseAnalysisResult>();

                foreach (var detail in response.ResponseDetails)
                {
                    var analysisText = BuildResponseText(detail);

                    if (!string.IsNullOrWhiteSpace(analysisText))
                    {
                        var analysisRequest = new AnalysisRequest
                        {
                            ResponseId = response.Id,
                            QuestionId = detail.QuestionId,
                            ResponseText = analysisText,
                            QuestionText = detail.Question?.Text ?? ""
                        };

                        var analysis = await _aiAnalysisService.AnalyzeCompleteResponseAsync(analysisRequest);
                        analysisResults.Add(analysis);
                    }
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
                    return Json(new { success = false, message = "Invalid analysis request." });

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
                        insights = analysis.Insights.Select(i => new
                        {
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
                _logger.LogError(ex, "Error analyzing response {ResponseId}", responseId);
                return Json(new { success = false, message = "Error analyzing response. Please try again." });
            }
        }

        #endregion

        #region Batch Analysis

        /// <summary>
        /// Process batch analysis for all responses in a questionnaire
        /// </summary>
        [HasPermission(Permissions.SurveyAnalysis.Analyze)]
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

                // Get all responses with ALL answer types
                var responses = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                            .ThenInclude(ra => ra.Answer)
                    .Where(r => r.QuestionnaireId == id)
                    .ToListAsync();

                var analysisRequests = new List<AnalysisRequest>();

                foreach (var response in responses)
                {
                    foreach (var detail in response.ResponseDetails)
                    {
                        // Build combined text from text response + selected answer texts
                        var analysisText = BuildResponseText(detail);

                        if (!string.IsNullOrWhiteSpace(analysisText))
                        {
                            analysisRequests.Add(new AnalysisRequest
                            {
                                ResponseId = response.Id,
                                QuestionId = detail.QuestionId,
                                ResponseText = analysisText,
                                QuestionText = detail.Question?.Text ?? ""
                            });
                        }
                    }
                }

                if (!analysisRequests.Any())
                {
                    TempData["WarningMessage"] = "No analyzable responses found. Responses must contain text or checkbox selections.";
                    return RedirectToAction(nameof(AnalyzeQuestionnaire), new { id });
                }

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
        [HasPermission(Permissions.SurveyAnalysis.Analyze)]
        [HttpPost]
        public async Task<IActionResult> ProcessBatchAnalysis(int questionnaireId)
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

                var analysisRequests = new List<AnalysisRequest>();

                foreach (var response in responses)
                {
                    foreach (var detail in response.ResponseDetails)
                    {
                        var analysisText = BuildResponseText(detail);

                        if (!string.IsNullOrWhiteSpace(analysisText))
                        {
                            analysisRequests.Add(new AnalysisRequest
                            {
                                ResponseId = response.Id,
                                QuestionId = detail.QuestionId,
                                ResponseText = analysisText,
                                QuestionText = detail.Question?.Text ?? ""
                            });
                        }
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
                return Json(new { success = false, message = "Error processing batch analysis. Please try again." });
            }
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Generate detailed analysis report for management
        /// </summary>
        [HasPermission(Permissions.SurveyAnalysis.Reports)]
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
        [HasPermission(Permissions.SurveyAnalysis.Reports)]
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

                var json = JsonSerializer.Serialize(analysisData, new JsonSerializerOptions { WriteIndented = true });
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
        [HasPermission(Permissions.SurveyAnalysis.Analyze)]
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
        /// Check Claude AI service health status (AJAX)
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
        /// Test Claude AI analysis with sample text (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> TestAnalysis(string sampleText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sampleText))
                    return Json(new { success = false, message = "Please provide sample text." });

                var analysisRequest = new AnalysisRequest
                {
                    ResponseId = 0,
                    QuestionId = 0,
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
                return Json(new { success = false, message = "Error performing test analysis. Please try again." });
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

        /// <summary>
        /// Builds analysis text from a ResponseDetail, combining text responses and selected answer texts.
        /// </summary>
        private string BuildResponseText(ResponseDetail detail)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(detail.TextResponse))
            {
                parts.Add(detail.TextResponse);
            }

            if (detail.ResponseAnswers != null && detail.ResponseAnswers.Any())
            {
                foreach (var ra in detail.ResponseAnswers)
                {
                    if (ra.Answer != null && !string.IsNullOrWhiteSpace(ra.Answer.Text))
                    {
                        parts.Add(ra.Answer.Text);
                    }
                }
            }

            return string.Join(". ", parts);
        }

        #region Case Management — Notes, Status, Action Plans, PDF Export, History

        // ─────────────────────────────────────────────
        // CASE NOTES CRUD
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get all notes for a response (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCaseNotes(int responseId)
        {
            var notes = await _context.CaseNotes
                .Where(n => n.ResponseId == responseId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.AuthorName,
                    n.AuthorEmail,
                    n.NoteText,
                    n.Category,
                    n.IsConfidential,
                    CreatedAt = n.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                    UpdatedAt = n.UpdatedAt.HasValue ? n.UpdatedAt.Value.ToString("MMM dd, yyyy HH:mm") : null
                })
                .ToListAsync();

            return Json(new { success = true, notes });
        }

        /// <summary>
        /// Add a case note (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddCaseNote(int responseId, string noteText, string category)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(noteText))
                    return Json(new { success = false, message = "Note text is required." });

                var currentUser = User.Identity?.Name ?? "Unknown";
                var currentEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;

                var note = new CaseNote
                {
                    ResponseId = responseId,
                    AuthorName = currentUser,
                    AuthorEmail = currentEmail,
                    NoteText = noteText.Trim(),
                    Category = string.IsNullOrWhiteSpace(category) ? "General" : category,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CaseNotes.Add(note);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    note = new
                    {
                        note.Id,
                        note.AuthorName,
                        note.AuthorEmail,
                        note.NoteText,
                        note.Category,
                        note.IsConfidential,
                        CreatedAt = note.CreatedAt.ToString("MMM dd, yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding case note for ResponseId {ResponseId}", responseId);
                return Json(new { success = false, message = "Failed to save note." });
            }
        }

        /// <summary>
        /// Delete a case note (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteCaseNote(int noteId)
        {
            try
            {
                var note = await _context.CaseNotes.FindAsync(noteId);
                if (note == null)
                    return Json(new { success = false, message = "Note not found." });

                _context.CaseNotes.Remove(note);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting case note {NoteId}", noteId);
                return Json(new { success = false, message = "Failed to delete note." });
            }
        }

        // ─────────────────────────────────────────────
        // CASE STATUS TRACKING
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get status history for a response (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCaseStatus(int responseId)
        {
            var history = await _context.CaseStatusEntries
                .Where(s => s.ResponseId == responseId)
                .OrderByDescending(s => s.ChangedAt)
                .Select(s => new
                {
                    s.Id,
                    Status = s.Status.ToString(),
                    StatusInt = (int)s.Status,
                    s.ChangedByName,
                    s.Reason,
                    ChangedAt = s.ChangedAt.ToString("MMM dd, yyyy HH:mm")
                })
                .ToListAsync();

            // Current status = most recent entry, or "New" if no entries
            var currentStatus = history.FirstOrDefault()?.Status ?? "New";
            var currentStatusInt = history.FirstOrDefault()?.StatusInt ?? 0;

            return Json(new { success = true, currentStatus, currentStatusInt, history });
        }

        /// <summary>
        /// Update case status (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateCaseStatus(int responseId, int newStatus, string reason)
        {
            try
            {
                if (!Enum.IsDefined(typeof(CaseStatusType), newStatus))
                    return Json(new { success = false, message = "Invalid status." });

                var currentUser = User.Identity?.Name ?? "Unknown";
                var currentEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;

                var entry = new CaseStatusEntry
                {
                    ResponseId = responseId,
                    Status = (CaseStatusType)newStatus,
                    ChangedByName = currentUser,
                    ChangedByEmail = currentEmail,
                    Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                    ChangedAt = DateTime.UtcNow
                };

                _context.CaseStatusEntries.Add(entry);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    entry = new
                    {
                        entry.Id,
                        Status = entry.Status.ToString(),
                        StatusInt = (int)entry.Status,
                        entry.ChangedByName,
                        entry.Reason,
                        ChangedAt = entry.ChangedAt.ToString("MMM dd, yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating case status for ResponseId {ResponseId}", responseId);
                return Json(new { success = false, message = "Failed to update status." });
            }
        }

        // ─────────────────────────────────────────────
        // ACTION PLANS CRUD
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get all action plans for a response (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetActionPlans(int responseId)
        {
            var plans = await _context.ActionPlans
                .Where(a => a.ResponseId == responseId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.ActionType,
                    a.Priority,
                    a.Status,
                    a.AssignedTo,
                    a.AssignedToEmail,
                    ScheduledDate = a.ScheduledDate.HasValue ? a.ScheduledDate.Value.ToString("MMM dd, yyyy HH:mm") : null,
                    CompletedDate = a.CompletedDate.HasValue ? a.CompletedDate.Value.ToString("MMM dd, yyyy HH:mm") : null,
                    a.CompletionNotes,
                    a.CreatedByName,
                    CreatedAt = a.CreatedAt.ToString("MMM dd, yyyy HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, plans });
        }

        /// <summary>
        /// Create a new action plan (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateActionPlan(
            int responseId, string title, string description,
            string actionType, string priority, string assignedTo,
            string assignedToEmail, string scheduledDate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(title))
                    return Json(new { success = false, message = "Title is required." });

                var currentUser = User.Identity?.Name ?? "Unknown";
                var currentEmail = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;

                var plan = new ActionPlan
                {
                    ResponseId = responseId,
                    Title = title.Trim(),
                    Description = description?.Trim(),
                    ActionType = string.IsNullOrWhiteSpace(actionType) ? "ImmediateContact" : actionType,
                    Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority,
                    Status = "Pending",
                    AssignedTo = assignedTo?.Trim(),
                    AssignedToEmail = assignedToEmail?.Trim(),
                    ScheduledDate = DateTime.TryParse(scheduledDate, out var sd) ? sd : null,
                    CreatedByName = currentUser,
                    CreatedByEmail = currentEmail,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ActionPlans.Add(plan);
                await _context.SaveChangesAsync();

                // Also auto-add a status entry for "InterventionScheduled" if not already
                var hasIntervention = await _context.CaseStatusEntries
                    .AnyAsync(s => s.ResponseId == responseId && s.Status == CaseStatusType.InterventionScheduled);
                if (!hasIntervention)
                {
                    _context.CaseStatusEntries.Add(new CaseStatusEntry
                    {
                        ResponseId = responseId,
                        Status = CaseStatusType.InterventionScheduled,
                        ChangedByName = currentUser,
                        ChangedByEmail = currentEmail,
                        Reason = $"Action plan created: {title}",
                        ChangedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }

                return Json(new
                {
                    success = true,
                    plan = new
                    {
                        plan.Id,
                        plan.Title,
                        plan.Description,
                        plan.ActionType,
                        plan.Priority,
                        plan.Status,
                        plan.AssignedTo,
                        ScheduledDate = plan.ScheduledDate?.ToString("MMM dd, yyyy HH:mm"),
                        plan.CreatedByName,
                        CreatedAt = plan.CreatedAt.ToString("MMM dd, yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating action plan for ResponseId {ResponseId}", responseId);
                return Json(new { success = false, message = "Failed to create action plan." });
            }
        }

        /// <summary>
        /// Update action plan status (AJAX) — mark as InProgress, Completed, Cancelled
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateActionPlanStatus(int planId, string status, string completionNotes)
        {
            try
            {
                var plan = await _context.ActionPlans.FindAsync(planId);
                if (plan == null)
                    return Json(new { success = false, message = "Action plan not found." });

                plan.Status = status;
                plan.UpdatedAt = DateTime.UtcNow;

                if (status == "Completed")
                {
                    plan.CompletedDate = DateTime.UtcNow;
                    plan.CompletionNotes = completionNotes?.Trim();
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, newStatus = plan.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating action plan {PlanId}", planId);
                return Json(new { success = false, message = "Failed to update action plan." });
            }
        }

        // ─────────────────────────────────────────────
        // RESPONDENT HISTORY TIMELINE
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get all responses from the same user across all questionnaires (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRespondentHistory(int responseId)
        {
            try
            {
                // Get the current response to find the user
                var currentResponse = await _context.Responses
                    .FirstOrDefaultAsync(r => r.Id == responseId);

                if (currentResponse == null)
                    return Json(new { success = false, message = "Response not found." });

                var userEmail = currentResponse.UserEmail;
                var userName = currentResponse.UserName;

                // Find all responses from this user
                var history = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .Where(r =>
                        (!string.IsNullOrEmpty(userEmail) && r.UserEmail == userEmail) ||
                        (!string.IsNullOrEmpty(userName) && r.UserName == userName))
                    .OrderByDescending(r => r.SubmissionDate)
                    .Select(r => new
                    {
                        r.Id,
                        QuestionnaireTitle = r.Questionnaire != null ? r.Questionnaire.Title : "Unknown",
                        r.QuestionnaireId,
                        SubmissionDate = r.SubmissionDate.ToString("MMM dd, yyyy HH:mm"),
                        SubmissionDateRaw = r.SubmissionDate,
                        TotalAnswered = r.ResponseDetails.Count,
                        TextResponses = r.ResponseDetails.Count(rd => !string.IsNullOrEmpty(rd.TextResponse)),
                        CheckboxResponses = r.ResponseDetails.Count(rd => rd.ResponseAnswers.Any()),
                        IsCurrent = r.Id == responseId,
                        // Get case status if any
                        LatestStatus = _context.CaseStatusEntries
                            .Where(s => s.ResponseId == r.Id)
                            .OrderByDescending(s => s.ChangedAt)
                            .Select(s => s.Status.ToString())
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    userName = userName ?? "Anonymous",
                    userEmail = userEmail ?? "No email",
                    totalResponses = history.Count,
                    history
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting respondent history for ResponseId {ResponseId}", responseId);
                return Json(new { success = false, message = "Failed to load history." });
            }
        }

        // ─────────────────────────────────────────────
        // PDF EXPORT — Case Report
        // ─────────────────────────────────────────────

        /// <summary>
        /// Export case as PDF report (download)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportCasePdf(int questionnaireId, int responseId)
        {
            try
            {
                var response = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                            .ThenInclude(ra => ra.Answer)
                    .FirstOrDefaultAsync(r => r.Id == responseId && r.QuestionnaireId == questionnaireId);

                if (response == null)
                    return NotFound();

                // Get analysis results
                var analysisResults = new List<ResponseAnalysisResult>();
                foreach (var detail in response.ResponseDetails)
                {
                    var analysisText = BuildResponseText(detail);
                    if (!string.IsNullOrWhiteSpace(analysisText))
                    {
                        var analysisRequest = new AnalysisRequest
                        {
                            ResponseId = response.Id,
                            QuestionId = detail.QuestionId,
                            ResponseText = analysisText,
                            QuestionText = detail.Question?.Text ?? ""
                        };
                        var analysis = await _aiAnalysisService.AnalyzeCompleteResponseAsync(analysisRequest);
                        analysisResults.Add(analysis);
                    }
                }

                // Get case notes
                var notes = await _context.CaseNotes
                    .Where(n => n.ResponseId == responseId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                // Get status history
                var statusHistory = await _context.CaseStatusEntries
                    .Where(s => s.ResponseId == responseId)
                    .OrderByDescending(s => s.ChangedAt)
                    .ToListAsync();

                // Get action plans
                var actionPlans = await _context.ActionPlans
                    .Where(a => a.ResponseId == responseId)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

                // Build PDF content as structured text (to be rendered by view)
                var reportData = new
                {
                    Response = response,
                    Analysis = analysisResults,
                    Notes = notes,
                    StatusHistory = statusHistory,
                    ActionPlans = actionPlans,
                    GeneratedAt = DateTime.UtcNow,
                    GeneratedBy = User.Identity?.Name ?? "System"
                };

                // Build plain text report for download
                var report = new System.Text.StringBuilder();
                report.AppendLine("═══════════════════════════════════════════════════");
                report.AppendLine("  NVKN — MENTAL HEALTH CASE REPORT (CONFIDENTIAL)");
                report.AppendLine("═══════════════════════════════════════════════════");
                report.AppendLine();
                report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                report.AppendLine($"Generated By: {User.Identity?.Name ?? "System"}");
                report.AppendLine($"Response ID: #{response.Id}");
                report.AppendLine($"Questionnaire: {response.Questionnaire?.Title}");
                report.AppendLine($"Respondent: {response.UserName ?? "Anonymous"}");
                report.AppendLine($"Submission Date: {response.SubmissionDate:yyyy-MM-dd HH:mm}");
                report.AppendLine();

                // Status
                report.AppendLine("── CASE STATUS ──────────────────────────────────");
                if (statusHistory.Any())
                {
                    var current = statusHistory.First();
                    report.AppendLine($"Current Status: {current.Status}");
                    report.AppendLine($"Last Updated: {current.ChangedAt:yyyy-MM-dd HH:mm} by {current.ChangedByName}");
                    if (!string.IsNullOrEmpty(current.Reason))
                        report.AppendLine($"Reason: {current.Reason}");
                }
                else
                {
                    report.AppendLine("Current Status: New");
                }
                report.AppendLine();

                // Analysis Results
                report.AppendLine("── AI ANALYSIS RESULTS ─────────────────────────");
                foreach (var analysis in analysisResults)
                {
                    report.AppendLine();
                    report.AppendLine($"Question: {analysis.QuestionText}");
                    report.AppendLine($"Response (Anonymized): {analysis.AnonymizedResponseText}");

                    if (analysis.SentimentAnalysis != null)
                    {
                        report.AppendLine($"Sentiment: {analysis.SentimentAnalysis.Sentiment}");
                        report.AppendLine($"  Positive: {Math.Round(analysis.SentimentAnalysis.PositiveScore * 100, 1)}%");
                        report.AppendLine($"  Neutral: {Math.Round(analysis.SentimentAnalysis.NeutralScore * 100, 1)}%");
                        report.AppendLine($"  Negative: {Math.Round(analysis.SentimentAnalysis.NegativeScore * 100, 1)}%");
                    }

                    if (analysis.RiskAssessment != null)
                    {
                        report.AppendLine($"Risk Level: {analysis.RiskAssessment.RiskLevel}");
                        report.AppendLine($"Risk Score: {Math.Round(analysis.RiskAssessment.RiskScore * 100, 0)}%");
                        if (analysis.RiskAssessment.RequiresImmediateAttention)
                            report.AppendLine("⚠ REQUIRES IMMEDIATE ATTENTION");
                        if (!string.IsNullOrEmpty(analysis.RiskAssessment.RecommendedAction))
                            report.AppendLine($"Recommended Action: {analysis.RiskAssessment.RecommendedAction}");
                        if (analysis.RiskAssessment.RiskIndicators?.Any() == true)
                            report.AppendLine($"Risk Indicators: {string.Join(", ", analysis.RiskAssessment.RiskIndicators)}");
                        if (analysis.RiskAssessment.ProtectiveFactors?.Any() == true)
                            report.AppendLine($"Protective Factors: {string.Join(", ", analysis.RiskAssessment.ProtectiveFactors)}");
                    }

                    if (analysis.KeyPhrases?.KeyPhrases?.Any() == true)
                        report.AppendLine($"Key Phrases: {string.Join(", ", analysis.KeyPhrases.KeyPhrases)}");

                    if (analysis.Insights?.Any() == true)
                    {
                        report.AppendLine("Workplace Insights:");
                        foreach (var insight in analysis.Insights)
                        {
                            report.AppendLine($"  [{insight.Category}] {insight.Issue}");
                            report.AppendLine($"    → {insight.RecommendedIntervention} (Priority: {insight.Priority})");
                        }
                    }
                    report.AppendLine("  ─ ─ ─ ─ ─ ─ ─ ─ ─ ─");
                }
                report.AppendLine();

                // Action Plans
                report.AppendLine("── ACTION PLANS ────────────────────────────────");
                if (actionPlans.Any())
                {
                    foreach (var plan in actionPlans)
                    {
                        report.AppendLine($"  [{plan.Priority}] {plan.Title} — {plan.Status}");
                        report.AppendLine($"    Type: {plan.ActionType}");
                        if (!string.IsNullOrEmpty(plan.AssignedTo))
                            report.AppendLine($"    Assigned To: {plan.AssignedTo}");
                        if (plan.ScheduledDate.HasValue)
                            report.AppendLine($"    Scheduled: {plan.ScheduledDate.Value:yyyy-MM-dd HH:mm}");
                        if (!string.IsNullOrEmpty(plan.Description))
                            report.AppendLine($"    Description: {plan.Description}");
                        if (plan.CompletedDate.HasValue)
                            report.AppendLine($"    Completed: {plan.CompletedDate.Value:yyyy-MM-dd HH:mm}");
                        report.AppendLine();
                    }
                }
                else
                {
                    report.AppendLine("  No action plans created yet.");
                }
                report.AppendLine();

                // Case Notes
                report.AppendLine("── CASE NOTES ──────────────────────────────────");
                if (notes.Any())
                {
                    foreach (var note in notes)
                    {
                        report.AppendLine($"  [{note.Category}] {note.CreatedAt:yyyy-MM-dd HH:mm} — {note.AuthorName}");
                        report.AppendLine($"    {note.NoteText}");
                        report.AppendLine();
                    }
                }
                else
                {
                    report.AppendLine("  No case notes recorded yet.");
                }
                report.AppendLine();

                // Status History
                report.AppendLine("── STATUS CHANGE LOG ───────────────────────────");
                if (statusHistory.Any())
                {
                    foreach (var entry in statusHistory)
                    {
                        report.AppendLine($"  {entry.ChangedAt:yyyy-MM-dd HH:mm} — {entry.Status} by {entry.ChangedByName}");
                        if (!string.IsNullOrEmpty(entry.Reason))
                            report.AppendLine($"    Reason: {entry.Reason}");
                    }
                }
                else
                {
                    report.AppendLine("  No status changes recorded.");
                }
                report.AppendLine();
                report.AppendLine("═══════════════════════════════════════════════════");
                report.AppendLine("  END OF REPORT — CONFIDENTIAL");
                report.AppendLine("═══════════════════════════════════════════════════");

                var bytes = System.Text.Encoding.UTF8.GetBytes(report.ToString());
                var fileName = $"CaseReport_Response{responseId}_{DateTime.UtcNow:yyyyMMdd_HHmm}.txt";

                return File(bytes, "text/plain", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting case PDF for ResponseId {ResponseId}", responseId);
                TempData["ErrorMessage"] = "Error exporting case report.";
                return RedirectToAction(nameof(ViewHighRiskResponse), new { questionnaireId, responseId });
            }
        }

        #endregion


































































































        #endregion
    }
}