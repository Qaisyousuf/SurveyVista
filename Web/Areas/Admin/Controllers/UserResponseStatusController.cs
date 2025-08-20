using Data;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Model;
using OfficeOpenXml;
using Services.Interaces;
using Web.ViewModel.QuestionnaireVM;

namespace Web.Areas.Admin.Controllers
{
    public class UserResponseStatusController : Controller
    {
        private readonly SurveyContext _context;
        private readonly IUserResponseRepository _userResponse;
        private readonly ILogger<UserResponseStatusController> _logger;

        public UserResponseStatusController(
            SurveyContext context,
            IUserResponseRepository userResponse,
            ILogger<UserResponseStatusController> logger)
        {
            _context = context;
            _userResponse = userResponse;
            _logger = logger;
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
    }
}