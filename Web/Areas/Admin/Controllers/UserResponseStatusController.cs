using Data;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Model;
using OfficeOpenXml;
using Services.Interaces;
using Web.ViewModel.QuestionnaireVM;
using Services.Interaces;
using Services.AIViewModel;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UserResponseStatusController : Controller
    {
        private readonly SurveyContext _context;
        private readonly IUserResponseRepository _userResponse;
        private readonly ILogger<UserResponseStatusController> _logger;
        private readonly IUserTrajectoryService _trajectoryService;

        public UserResponseStatusController(
            SurveyContext context,
            IUserResponseRepository userResponse,
            ILogger<UserResponseStatusController> logger, IUserTrajectoryService trajectoryService)
        {
            _context = context;
            _userResponse = userResponse;
            _logger = logger;
            _trajectoryService = trajectoryService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var usersWithQuestionnaires = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .GroupBy(r => r.UserEmail)
                    .Select(g => new UserResponsesViewModel
                    {
                        UserName = g.FirstOrDefault().UserName,
                        UserEmail = g.Key,
                        Responses = g.Select(r => new Response
                        {
                            Questionnaire = r.Questionnaire
                        }).Distinct().ToList()
                    })
                    .ToListAsync();

                return View(usersWithQuestionnaires);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user responses");
                TempData["Error"] = "Error loading user responses. Please try again.";
                return View(new List<UserResponsesViewModel>());
            }
        }

        public async Task<IActionResult> UserResponsesStatus(string userEmail)
        {
            try
            {
                var responses = await _context.Responses
                    .Include(r => r.Questionnaire)
                        .ThenInclude(q => q.Questions.OrderBy(qu => qu.Id))
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                            .ThenInclude(q => q.Answers)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .Where(r => r.UserEmail == userEmail)
                    .ToListAsync();

                if (responses == null || !responses.Any())
                {
                    TempData["Warning"] = "No responses found for this user.";
                    return RedirectToAction(nameof(Index));
                }

                var userName = responses.First().UserName;

                var viewModel = new UserResponsesViewModel
                {
                    UserName = userName,
                    UserEmail = userEmail,
                    Responses = responses
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user responses for {UserEmail}", userEmail);
                TempData["Error"] = "Error loading user response details.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected(List<string> selectedEmails)
        {
            if (selectedEmails == null || !selectedEmails.Any())
            {
                TempData["Warning"] = "No users selected for deletion.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _logger.LogInformation("Attempting to delete responses for {Count} users: {Emails}",
                    selectedEmails.Count, string.Join(", ", selectedEmails));

                var responsesToDelete = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .Where(r => selectedEmails.Contains(r.UserEmail))
                    .ToListAsync();

                if (!responsesToDelete.Any())
                {
                    TempData["Warning"] = "No responses found for the selected users.";
                    return RedirectToAction(nameof(Index));
                }

                // Remove related data first to avoid foreign key constraints
                foreach (var response in responsesToDelete)
                {
                    if (response.ResponseDetails != null)
                    {
                        foreach (var detail in response.ResponseDetails)
                        {
                            if (detail.ResponseAnswers != null)
                            {
                                _context.ResponseAnswers.RemoveRange(detail.ResponseAnswers);
                            }
                        }
                        _context.ResponseDetails.RemoveRange(response.ResponseDetails);
                    }
                }

                _context.Responses.RemoveRange(responsesToDelete);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted {Count} responses for {UserCount} users",
                    responsesToDelete.Count, selectedEmails.Count);

                TempData["Success"] = $"Successfully deleted responses for {selectedEmails.Count} user{(selectedEmails.Count > 1 ? "s" : "")} ({responsesToDelete.Count} response{(responsesToDelete.Count > 1 ? "s" : "")} total).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting responses for users: {Emails}", string.Join(", ", selectedEmails));
                TempData["Error"] = "Error deleting user responses. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserResponses(string userEmail)
        {
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["Error"] = "User email is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var responsesToDelete = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .Where(r => r.UserEmail == userEmail)
                    .ToListAsync();

                if (!responsesToDelete.Any())
                {
                    TempData["Warning"] = "No responses found for this user.";
                    return RedirectToAction(nameof(Index));
                }

                // Remove related data first
                foreach (var response in responsesToDelete)
                {
                    if (response.ResponseDetails != null)
                    {
                        foreach (var detail in response.ResponseDetails)
                        {
                            if (detail.ResponseAnswers != null)
                            {
                                _context.ResponseAnswers.RemoveRange(detail.ResponseAnswers);
                            }
                        }
                        _context.ResponseDetails.RemoveRange(response.ResponseDetails);
                    }
                }

                _context.Responses.RemoveRange(responsesToDelete);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted {Count} responses for user {UserEmail}",
                    responsesToDelete.Count, userEmail);

                TempData["Success"] = $"Successfully deleted all responses for user {userEmail}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting responses for user {UserEmail}", userEmail);
                TempData["Error"] = "Error deleting user responses. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> GenerateReport(string userEmail, string format)
        {
            try
            {
                var responses = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                            .ThenInclude(q => q.Answers)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .Where(r => r.UserEmail == userEmail)
                    .ToListAsync();

                if (responses == null || !responses.Any())
                {
                    TempData["Warning"] = "No responses found for this user.";
                    return RedirectToAction(nameof(Index));
                }

                switch (format.ToLower())
                {
                    case "pdf":
                        return GeneratePdfReport(responses);
                    case "excel":
                        return GenerateExcelReport(responses);
                    default:
                        TempData["Error"] = "Unsupported report format.";
                        return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report for user {UserEmail}", userEmail);
                TempData["Error"] = "Error generating report. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        private IActionResult GeneratePdfReport(List<Response> responses)
        {
            var userName = responses.First().UserName;
            var userEmail = responses.First().UserEmail;

            var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 50, 50, 25, 25);
            var writer = PdfWriter.GetInstance(document, stream);
            writer.CloseStream = false;

            document.Open();

            // Add a title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
            var title = new Paragraph($"Report for {userName} ({userEmail})", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(title);

            // Add a logo
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
            if (System.IO.File.Exists(logoPath))
            {
                var logo = Image.GetInstance(logoPath);
                logo.ScaleToFit(100f, 100f);
                logo.Alignment = Image.ALIGN_CENTER;
                document.Add(logo);
            }

            // Add a table for each response
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.WHITE);
            var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.BLACK);

            foreach (var response in responses)
            {
                var table = new PdfPTable(2)
                {
                    WidthPercentage = 100,
                    SpacingBefore = 20,
                    SpacingAfter = 20
                };
                table.SetWidths(new float[] { 1, 3 });

                var cell = new PdfPCell(new Phrase($"Survey: {response.Questionnaire.Title}", headerFont))
                {
                    Colspan = 2,
                    BackgroundColor = new BaseColor(0, 150, 0),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 10
                };
                table.AddCell(cell);

                table.AddCell(new PdfPCell(new Phrase("Submitted on:", cellFont)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(response.SubmissionDate.ToString(), cellFont)) { Padding = 5 });

                foreach (var detail in response.ResponseDetails)
                {
                    table.AddCell(new PdfPCell(new Phrase("Question:", cellFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(detail.Question.Text, cellFont)) { Padding = 5 });

                    if (detail.QuestionType == QuestionType.Text || detail.QuestionType == QuestionType.Slider || detail.QuestionType == QuestionType.Open_ended)
                    {
                        table.AddCell(new PdfPCell(new Phrase("Answer:", cellFont)) { Padding = 5 });
                        table.AddCell(new PdfPCell(new Phrase(detail.TextResponse, cellFont)) { Padding = 5 });
                    }
                    else
                    {
                        table.AddCell(new PdfPCell(new Phrase("Answers:", cellFont)) { Padding = 5 });
                        var answers = string.Join(", ", detail.ResponseAnswers.Select(a => detail.Question.Answers.FirstOrDefault(ans => ans.Id == a.AnswerId)?.Text));

                        // Include "Other" text if available
                        if (!string.IsNullOrEmpty(detail.OtherText))
                        {
                            answers += string.IsNullOrEmpty(answers)
                                ? $"Other: {detail.OtherText}"
                                : $"; Other: {detail.OtherText}";
                        }

                        table.AddCell(new PdfPCell(new Phrase(answers, cellFont)) { Padding = 5 });
                    }
                }

                document.Add(table);
            }

            document.Close();
            writer.Close();

            stream.Position = 0;
            return File(stream, "application/pdf", $"{userName}_report.pdf");
        }

        private IActionResult GenerateExcelReport(List<Response> responses)
        {
            var userName = responses.First().UserName;
            var userEmail = responses.First().UserEmail;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Report");

                // Add a logo
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = new FileInfo(logoPath);
                    var picture = worksheet.Drawings.AddPicture("Logo", logo);
                    picture.SetPosition(0, 0, 0, 0);
                    picture.SetSize(300, 70);
                }

                // Add a title
                worksheet.Cells[6, 1].Value = $"Report for {userName} ({userEmail})";
                worksheet.Cells[6, 1, 6, 4].Merge = true;
                worksheet.Cells[6, 1, 6, 4].Style.Font.Size = 18;
                worksheet.Cells[6, 1, 6, 4].Style.Font.Bold = true;
                worksheet.Cells[6, 1, 6, 4].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Add headers
                worksheet.Cells[7, 1].Value = "Survey";
                worksheet.Cells[7, 2].Value = "Submitted on";
                worksheet.Cells[7, 3].Value = "Question";
                worksheet.Cells[7, 4].Value = "Response";

                using (var range = worksheet.Cells[7, 1, 7, 4])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Add data
                var row = 8;
                foreach (var response in responses)
                {
                    worksheet.Cells[row, 1].Value = response.Questionnaire.Title;
                    worksheet.Cells[row, 2].Value = response.SubmissionDate.ToString();
                    row++;

                    foreach (var detail in response.ResponseDetails)
                    {
                        worksheet.Cells[row, 3].Value = detail.Question.Text;

                        if (detail.QuestionType == QuestionType.Text || detail.QuestionType == QuestionType.Slider || detail.QuestionType == QuestionType.Open_ended)
                        {
                            worksheet.Cells[row, 4].Value = detail.TextResponse;
                        }
                        else
                        {
                            var answers = string.Join(", ", detail.ResponseAnswers.Select(a => detail.Question.Answers.FirstOrDefault(ans => ans.Id == a.AnswerId)?.Text));

                            // Include "Other" text if available
                            if (!string.IsNullOrEmpty(detail.OtherText))
                            {
                                answers += string.IsNullOrEmpty(answers)
                                    ? $"Other: {detail.OtherText}"
                                    : $"; Other: {detail.OtherText}";
                            }

                            worksheet.Cells[row, 4].Value = answers;
                        }
                        row++;
                    }
                    row++;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{userName}_report.xlsx");
            }
        }

        public async Task<IActionResult> GenerateQuestionnairePdfReport(int questionnaireId)
        {
            try
            {
                var response = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                            .ThenInclude(q => q.Answers)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .FirstOrDefaultAsync(r => r.QuestionnaireId == questionnaireId);

                if (response == null)
                {
                    TempData["Warning"] = "No response found for this questionnaire.";
                    return RedirectToAction(nameof(Index));
                }

                return GeneratePdfReportForQuestionnaire(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating questionnaire PDF report for {QuestionnaireId}", questionnaireId);
                TempData["Error"] = "Error generating PDF report. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        private IActionResult GeneratePdfReportForQuestionnaire(Response response)
        {
            var userName = response.UserName;
            var userEmail = response.UserEmail;

            var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 50, 50, 25, 25);
            var writer = PdfWriter.GetInstance(document, stream);
            writer.CloseStream = false;

            document.Open();

            // Add a title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
            var title = new Paragraph($"Report for {response.Questionnaire.Title}", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(title);

            // Add a logo
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
            if (System.IO.File.Exists(logoPath))
            {
                var logo = Image.GetInstance(logoPath);
                logo.ScaleToFit(100f, 100f);
                logo.Alignment = Image.ALIGN_CENTER;
                document.Add(logo);
            }

            // Add a table
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.WHITE);
            var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.BLACK);
            var table = new PdfPTable(2)
            {
                WidthPercentage = 100,
                SpacingBefore = 20,
                SpacingAfter = 20
            };
            table.SetWidths(new float[] { 1, 3 });

            var cellForResponse = new PdfPCell(new Phrase($"{response.UserName} ({response.UserEmail})", headerFont))
            {
                Colspan = 2,
                BackgroundColor = new BaseColor(0, 150, 0),
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 10
            };

            table.AddCell(cellForResponse);
            var cell = new PdfPCell(new Phrase($"Survey: {response.Questionnaire.Title}", headerFont))
            {
                Colspan = 2,
                BackgroundColor = new BaseColor(0, 150, 0),
                HorizontalAlignment = Element.ALIGN_CENTER,
                Padding = 10
            };
            table.AddCell(cell);

            table.AddCell(new PdfPCell(new Phrase("Submitted on:", cellFont)) { Padding = 5 });
            table.AddCell(new PdfPCell(new Phrase(response.SubmissionDate.ToString(), cellFont)) { Padding = 5 });

            foreach (var detail in response.ResponseDetails)
            {
                table.AddCell(new PdfPCell(new Phrase("Question:", cellFont)) { Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase(detail.Question.Text, cellFont)) { Padding = 5 });

                if (detail.QuestionType == QuestionType.Text || detail.QuestionType == QuestionType.Slider || detail.QuestionType == QuestionType.Open_ended)
                {
                    table.AddCell(new PdfPCell(new Phrase("Answer:", cellFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(detail.TextResponse, cellFont)) { Padding = 5 });
                }
                else
                {
                    table.AddCell(new PdfPCell(new Phrase("Answers:", cellFont)) { Padding = 5 });
                    var answers = string.Join(", ", detail.ResponseAnswers.Select(a => detail.Question.Answers.FirstOrDefault(ans => ans.Id == a.AnswerId)?.Text));

                    // Include "Other" text if available
                    if (!string.IsNullOrEmpty(detail.OtherText))
                    {
                        answers += string.IsNullOrEmpty(answers)
                            ? $"Other: {detail.OtherText}"
                            : $"; Other: {detail.OtherText}";
                    }

                    table.AddCell(new PdfPCell(new Phrase(answers, cellFont)) { Padding = 5 });
                }
            }

            document.Add(table);
            document.Close();
            writer.Close();

            stream.Position = 0;
            return File(stream, "application/pdf", $"{response.Questionnaire.Title}_{userEmail}.pdf");
        }

        public async Task<IActionResult> GenerateQuestionnaireExcelReport(int questionnaireId)
        {
            try
            {
                var response = await _context.Responses
                    .Include(r => r.Questionnaire)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                            .ThenInclude(q => q.Answers)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .FirstOrDefaultAsync(r => r.QuestionnaireId == questionnaireId);

                if (response == null)
                {
                    TempData["Warning"] = "No response found for this questionnaire.";
                    return RedirectToAction(nameof(Index));
                }

                return GenerateExcelReportForQuestionnaire(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating questionnaire Excel report for {QuestionnaireId}", questionnaireId);
                TempData["Error"] = "Error generating Excel report. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        private IActionResult GenerateExcelReportForQuestionnaire(Response response)
        {
            var userName = response.UserName;
            var userEmail = response.UserEmail;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Report");

                // Add a logo
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = new FileInfo(logoPath);
                    var picture = worksheet.Drawings.AddPicture("Logo", logo);
                    picture.SetPosition(0, 0, 2, 0);
                    picture.SetSize(300, 60);
                }

                // Add user details
                worksheet.Cells[5, 1].Value = $"{userName} ({userEmail})";
                worksheet.Cells[5, 1, 5, 4].Merge = true;
                worksheet.Cells[5, 1, 5, 4].Style.Font.Size = 15;
                worksheet.Cells[5, 1, 5, 4].Style.Font.Bold = true;
                worksheet.Cells[5, 1, 5, 4].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Add a title
                worksheet.Cells[6, 1].Value = $"Report for {response.Questionnaire.Title}";
                worksheet.Cells[6, 1, 6, 4].Merge = true;
                worksheet.Cells[6, 1, 6, 4].Style.Font.Size = 18;
                worksheet.Cells[6, 1, 6, 4].Style.Font.Bold = true;
                worksheet.Cells[6, 1, 6, 4].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Add headers
                worksheet.Cells[7, 1].Value = "Survey";
                worksheet.Cells[7, 2].Value = "Submitted on";
                worksheet.Cells[7, 3].Value = "Question";
                worksheet.Cells[7, 4].Value = "Response";

                using (var range = worksheet.Cells[7, 1, 7, 4])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Add data
                var row = 8;
                worksheet.Cells[row, 1].Value = response.Questionnaire.Title;
                worksheet.Cells[row, 2].Value = response.SubmissionDate.ToString();
                row++;

                foreach (var detail in response.ResponseDetails)
                {
                    worksheet.Cells[row, 3].Value = detail.Question.Text;

                    if (detail.QuestionType == QuestionType.Text || detail.QuestionType == QuestionType.Slider || detail.QuestionType == QuestionType.Open_ended)
                    {
                        worksheet.Cells[row, 4].Value = detail.TextResponse;
                    }
                    else
                    {
                        var answers = string.Join(", ", detail.ResponseAnswers.Select(a => detail.Question.Answers.FirstOrDefault(ans => ans.Id == a.AnswerId)?.Text));

                        // Include "Other" text if available
                        if (!string.IsNullOrEmpty(detail.OtherText))
                        {
                            answers += string.IsNullOrEmpty(answers)
                                ? $"Other: {detail.OtherText}"
                                : $"; Other: {detail.OtherText}";
                        }

                        worksheet.Cells[row, 4].Value = answers;
                    }
                    row++;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{response.Questionnaire.Title}_{userEmail}.xlsx");
            }
        }

        // API endpoint to check if user responses exist
        [HttpGet]
        public async Task<IActionResult> CheckUserResponsesExist(string userEmail)
        {
            var exists = await _context.Responses.AnyAsync(r => r.UserEmail == userEmail);
            return Json(new { exists });
        }

        // API endpoint to get user response count
        [HttpGet]
        public async Task<IActionResult> GetUserResponseCount()
        {
            var count = await _context.Responses.GroupBy(r => r.UserEmail).CountAsync();
            return Json(new { count });
        }

        /// <summary>
        /// Analyze wellness trajectory for a user (AJAX)
        /// Returns cached result or calls Claude API if new responses exist
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AnalyzeTrajectory(string userEmail)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userEmail))
                    return Json(new { success = false, message = "User email is required." });

                // Check if user has any responses
                var hasResponses = await _context.Responses.AnyAsync(r => r.UserEmail == userEmail);
                if (!hasResponses)
                    return Json(new { success = false, message = "No responses found for this user." });

                var result = await _trajectoryService.GetOrAnalyzeTrajectoryAsync(userEmail);

                return Json(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error analyzing trajectory: " + ex.Message });
            }
        }

        /// <summary>
        /// Force re-analyze trajectory (ignores cache)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReanalyzeTrajectory(string userEmail)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userEmail))
                    return Json(new { success = false, message = "User email is required." });

                var result = await _trajectoryService.ForceReanalyzeTrajectoryAsync(userEmail);

                return Json(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error re-analyzing: " + ex.Message });
            }
        }

        /// <summary>
        /// Check cache status (AJAX) — useful for showing "new data available" badge
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CheckTrajectoryStatus(string userEmail)
        {
            try
            {
                var (hasCache, isStale, cachedCount, currentCount) =
                    await _trajectoryService.CheckCacheStatusAsync(userEmail);

                return Json(new
                {
                    success = true,
                    hasCache,
                    isStale,
                    cachedCount,
                    currentCount,
                    newResponses = currentCount - cachedCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Export trajectory analysis as a professional PDF report
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportTrajectoryPdf(string userEmail)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userEmail))
                    return BadRequest("User email is required.");

                var analysis = await _trajectoryService.GetOrAnalyzeTrajectoryAsync(userEmail);

                var userResponse = await _context.Responses
                    .FirstOrDefaultAsync(r => r.UserEmail == userEmail);
                var userName = userResponse?.UserName ?? "Unknown";

                // ── Build PDF ──
                var stream = new MemoryStream();
                var document = new Document(PageSize.A4, 40, 40, 40, 40);
                var writer = PdfWriter.GetInstance(document, stream);
                writer.CloseStream = false;
                document.Open();

                // Fonts
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, new BaseColor(30, 41, 59));
                var h1Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(30, 41, 59));
                var h2Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(71, 85, 105));
                var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(100, 116, 139));
                var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.BLACK);
                var accentFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(79, 70, 229));

                // Colors
                var primaryBg = new BaseColor(238, 242, 255);    // indigo-50
                var greenBg = new BaseColor(236, 253, 245);       // green-50
                var redBg = new BaseColor(254, 242, 242);          // red-50
                var yellowBg = new BaseColor(254, 252, 232);       // yellow-50
                var grayBg = new BaseColor(248, 250, 252);         // slate-50
                var darkText = new BaseColor(30, 41, 59);
                var greenText = new BaseColor(22, 163, 74);
                var redText = new BaseColor(220, 38, 38);
                var yellowText = new BaseColor(161, 98, 7);

                // ── Logo ──
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = Image.GetInstance(logoPath);
                    logo.ScaleToFit(120f, 60f);
                    logo.Alignment = Image.ALIGN_LEFT;
                    document.Add(logo);
                    document.Add(new Paragraph(" ") { SpacingAfter = 5 });
                }

                // ── Title ──
                document.Add(new Paragraph("Wellness Trajectory Analysis Report", titleFont) { SpacingAfter = 5 });
                document.Add(new Paragraph("CONFIDENTIAL — For Professional Use Only",
                    FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(220, 38, 38)))
                { SpacingAfter = 15 });

                // ── User Info Table ──
                var infoTable = new PdfPTable(2) { WidthPercentage = 100, SpacingAfter = 15 };
                infoTable.SetWidths(new float[] { 1, 2 });
                AddInfoRow(infoTable, "Respondent:", userName, boldFont, bodyFont, grayBg);
                AddInfoRow(infoTable, "Email:", userEmail, boldFont, bodyFont, grayBg);
                AddInfoRow(infoTable, "Responses Analyzed:", analysis.TotalResponsesAnalyzed.ToString(), boldFont, bodyFont, grayBg);
                AddInfoRow(infoTable, "Report Generated:", DateTime.UtcNow.ToString("MMMM dd, yyyy HH:mm UTC"), boldFont, bodyFont, grayBg);
                document.Add(infoTable);

                // ── Trajectory Overview ──
                AddSectionHeader(document, "TRAJECTORY OVERVIEW", h1Font);

                var overviewTable = new PdfPTable(4) { WidthPercentage = 100, SpacingAfter = 10 };
                overviewTable.SetWidths(new float[] { 1, 1, 1, 1 });

                AddMetricCell(overviewTable, "Direction", analysis.TrajectoryDirection, accentFont, smallFont, primaryBg);
                AddMetricCell(overviewTable, "Wellness Score", $"{analysis.TrajectoryScore}/100", accentFont, smallFont, primaryBg);
                AddMetricCell(overviewTable, "Score Change", $"{(analysis.ScoreChange >= 0 ? "+" : "")}{analysis.ScoreChange}", accentFont, smallFont,
                    analysis.ScoreChange >= 0 ? greenBg : redBg);
                AddMetricCell(overviewTable, "Risk Level", analysis.OverallRiskLevel,
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10,
                        analysis.OverallRiskLevel == "Critical" || analysis.OverallRiskLevel == "High" ? redText :
                        analysis.OverallRiskLevel == "Moderate" ? yellowText : greenText),
                    smallFont,
                    analysis.OverallRiskLevel == "Critical" || analysis.OverallRiskLevel == "High" ? redBg :
                    analysis.OverallRiskLevel == "Moderate" ? yellowBg : greenBg);
                document.Add(overviewTable);

                // Executive Summary
                document.Add(new Paragraph("Executive Summary", h2Font) { SpacingBefore = 8, SpacingAfter = 5 });
                document.Add(new Paragraph(analysis.ExecutiveSummary, bodyFont) { SpacingAfter = 15 });

                // ── Response History ──
                AddSectionHeader(document, "RESPONSE HISTORY", h1Font);

                foreach (var snap in analysis.ResponseSnapshots)
                {
                    var snapTable = new PdfPTable(4) { WidthPercentage = 100, SpacingAfter = 8 };
                    snapTable.SetWidths(new float[] { 1.5f, 0.8f, 0.8f, 0.8f });

                    var nameCell = new PdfPCell(new Phrase($"{snap.ResponseDate} — {snap.QuestionnaireName}", boldFont))
                    { Colspan = 4, BackgroundColor = grayBg, Padding = 8, BorderColor = new BaseColor(226, 232, 240) };
                    snapTable.AddCell(nameCell);

                    AddSnapCell(snapTable, $"Score: {snap.WellnessScore}/100", bodyFont, BaseColor.WHITE);
                    AddSnapCell(snapTable, $"Risk: {snap.RiskLevel}", bodyFont,
                        snap.RiskLevel == "Critical" || snap.RiskLevel == "High" ? redBg :
                        snap.RiskLevel == "Moderate" ? yellowBg : greenBg);
                    AddSnapCell(snapTable, $"Sentiment: {snap.SentimentLabel}", bodyFont, BaseColor.WHITE);
                    AddSnapCell(snapTable, snap.KeyThemes.Any() ? string.Join(", ", snap.KeyThemes) : "—", smallFont, BaseColor.WHITE);

                    var summCell = new PdfPCell(new Phrase(snap.BriefSummary, smallFont))
                    { Colspan = 4, Padding = 6, BorderColor = new BaseColor(226, 232, 240), PaddingBottom = 8 };
                    snapTable.AddCell(summCell);

                    document.Add(snapTable);
                }

                // ── Pattern Insights ──
                if (analysis.PatternInsights.Any())
                {
                    AddSectionHeader(document, "PATTERN INSIGHTS", h1Font);
                    foreach (var p in analysis.PatternInsights)
                    {
                        var status = p.StillPresent ? "Still Present" : "Resolved";
                        var statusColor = p.StillPresent ? redText : greenText;
                        document.Add(new Paragraph($"[{p.Severity.ToUpper()}] {p.Pattern}", boldFont) { SpacingAfter = 2 });
                        document.Add(new Paragraph($"First seen: {p.FirstSeen} — {status}",
                            FontFactory.GetFont(FontFactory.HELVETICA, 9, statusColor))
                        { SpacingAfter = 8 });
                    }
                }

                // ── Strengths ──
                if (analysis.StrengthFactors.Any())
                {
                    AddSectionHeader(document, "PROTECTIVE STRENGTHS", h1Font);
                    foreach (var s in analysis.StrengthFactors)
                    {
                        document.Add(new Paragraph($"✓  {s.Factor}",
                            FontFactory.GetFont(FontFactory.HELVETICA, 10, greenText))
                        { SpacingAfter = 5 });
                    }
                }

                // ── Concerns ──
                if (analysis.ConcernFactors.Any())
                {
                    document.Add(new Paragraph(" ") { SpacingAfter = 5 });
                    AddSectionHeader(document, "AREAS OF CONCERN", h1Font);
                    foreach (var c in analysis.ConcernFactors)
                    {
                        document.Add(new Paragraph($"⚠  [{c.Urgency.ToUpper()}] {c.Concern}",
                            FontFactory.GetFont(FontFactory.HELVETICA, 10,
                            c.Urgency == "Immediate" ? redText : darkText))
                        { SpacingAfter = 5 });
                    }
                }

                // ── Recommendations ──
                if (analysis.Recommendations.Any())
                {
                    document.Add(new Paragraph(" ") { SpacingAfter = 5 });
                    AddSectionHeader(document, "AI RECOMMENDATIONS", h1Font);
                    int recNum = 0;
                    foreach (var r in analysis.Recommendations)
                    {
                        recNum++;
                        document.Add(new Paragraph($"{recNum}. [{r.Priority.ToUpper()}] {r.Action}", boldFont) { SpacingAfter = 2 });
                        document.Add(new Paragraph($"    Category: {r.Category}", smallFont) { SpacingAfter = 8 });
                    }
                }

                // ── Narrative ──
                document.Add(new Paragraph(" ") { SpacingAfter = 5 });
                AddSectionHeader(document, "PROFESSIONAL NARRATIVE", h1Font);
                document.Add(new Paragraph(analysis.TimelineNarrative,
                    FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10, darkText))
                { SpacingAfter = 15 });

                // ── Detailed Analysis ──
                AddSectionHeader(document, "DETAILED CLINICAL ANALYSIS", h1Font);
                document.Add(new Paragraph(analysis.DetailedAnalysis, bodyFont) { SpacingAfter = 15 });

                // ── Footer ──
                var footerTable = new PdfPTable(1) { WidthPercentage = 100, SpacingBefore = 20 };
                var footerCell = new PdfPCell(new Phrase(
                    "This report was generated by AI-powered wellness analysis. It is intended for professional mental health consultants and should be treated as CONFIDENTIAL.",
                    FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(148, 163, 184))))
                {
                    BackgroundColor = grayBg,
                    Padding = 10,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    BorderColor = new BaseColor(226, 232, 240)
                };
                footerTable.AddCell(footerCell);
                document.Add(footerTable);

                document.Close();
                writer.Close();

                stream.Position = 0;
                var fileName = $"WellnessTrajectory_{userName.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}.pdf";
                return File(stream, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error exporting trajectory report: " + ex.Message;
                return RedirectToAction(nameof(UserResponsesStatus), new { userEmail });
            }
        }

        // ── PDF Helper Methods (add these as private methods in the controller) ──

        private void AddSectionHeader(Document doc, string text, Font font)
        {
            var p = new Paragraph(text, font) { SpacingBefore = 10, SpacingAfter = 8 };
            doc.Add(p);
            // Add a thin line under the header
            var line = new PdfPTable(1) { WidthPercentage = 100, SpacingAfter = 8 };
            var lineCell = new PdfPCell() { FixedHeight = 2, BackgroundColor = new BaseColor(79, 70, 229), Border = 0 };
            line.AddCell(lineCell);
            doc.Add(line);
        }

        private void AddInfoRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont, BaseColor bg)
        {
            var lCell = new PdfPCell(new Phrase(label, labelFont))
            { BackgroundColor = bg, Padding = 6, BorderColor = new BaseColor(226, 232, 240) };
            var vCell = new PdfPCell(new Phrase(value, valueFont))
            { Padding = 6, BorderColor = new BaseColor(226, 232, 240) };
            table.AddCell(lCell);
            table.AddCell(vCell);
        }

        private void AddMetricCell(PdfPTable table, string label, string value, Font valueFont, Font labelFont, BaseColor bg)
        {
            var cell = new PdfPCell()
            {
                BackgroundColor = bg,
                Padding = 10,
                HorizontalAlignment = Element.ALIGN_CENTER,
                BorderColor = new BaseColor(226, 232, 240)
            };
            cell.AddElement(new Paragraph(value, valueFont) { Alignment = Element.ALIGN_CENTER });
            cell.AddElement(new Paragraph(label, labelFont) { Alignment = Element.ALIGN_CENTER, SpacingBefore = 2 });
            table.AddCell(cell);
        }

        private void AddSnapCell(PdfPTable table, string text, Font font, BaseColor bg)
        {
            var cell = new PdfPCell(new Phrase(text, font))
            { BackgroundColor = bg, Padding = 6, BorderColor = new BaseColor(226, 232, 240) };
            table.AddCell(cell);
        }

    }
}