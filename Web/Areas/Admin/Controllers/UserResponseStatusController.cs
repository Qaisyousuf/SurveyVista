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

        public UserResponseStatusController(SurveyContext context,IUserResponseRepository userResponse)
        {
            _context = context;
            _userResponse = userResponse;
        }
        public async Task<IActionResult> Index()
        {
            var usersWithQuestionnaires = await _context.Responses
            .Include(r => r.Questionnaire)
            .GroupBy(r => r.UserEmail)
            .Select(g => new UserResponsesViewModel
            {
                UserName = g.FirstOrDefault().UserName, // Display the first username found for the email
                UserEmail = g.Key,
                Responses = g.Select(r => new Response
                {
                    Questionnaire = r.Questionnaire
                }).Distinct().ToList()
            })
            .ToListAsync();

            return View(usersWithQuestionnaires);
        }


        public async Task<IActionResult> UserResponsesStatus(string userEmail)
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
                return NotFound();
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



        [HttpPost]
        public async Task<IActionResult> DeleteSelected(string[] selectedEmails)
        {
            if (selectedEmails == null || selectedEmails.Length == 0)
            {
                return RedirectToAction(nameof(Index));
            }

            var responsesToDelete = await _context.Responses
                .Where(r => selectedEmails.Contains(r.UserEmail))
                .ToListAsync();

            if (responsesToDelete.Any())
            {
                _context.Responses.RemoveRange(responsesToDelete);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }



        public async Task<IActionResult> GenerateReport(string userEmail, string format)
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
                return NotFound();
            }

            switch (format.ToLower())
            {
                case "pdf":
                    return GeneratePdfReport(responses);
                case "excel":
                    return GenerateExcelReport(responses);
                default:
                    return BadRequest("Unsupported report format.");
            }
        }


        private IActionResult GeneratePdfReport(List<Response> responses)
        {
            var userName = responses.First().UserName;
            var userEmail = responses.First().UserEmail;

            var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 50, 50, 25, 25);
            var writer = PdfWriter.GetInstance(document, stream);
            writer.CloseStream = false; // Prevent the stream from being closed when the document is closed

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
                    picture.SetSize(300, 70); // Adjust the size as needed
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
                return NotFound();
            }

            return GeneratePdfReportForQuestionnaire(response);
        }

        private IActionResult GeneratePdfReportForQuestionnaire(Response response)
        {
            var userName = response.UserName;
            var userEmail = response.UserEmail;

            var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 50, 50, 25, 25);
            var writer = PdfWriter.GetInstance(document, stream);
            writer.CloseStream = false; // Prevent the stream from being closed when the document is closed

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
                return NotFound();
            }

            return GenerateExcelReportForQuestionnaire(response);
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
                    picture.SetSize(300, 60); // Adjust the size as needed
                }

                // Add user details
                worksheet.Cells[5, 1].Value = $"{userName} ({userEmail})";
                worksheet.Cells[5, 1, 5, 4].Merge = true;
                worksheet.Cells[5, 1, 5, 4].Style.Font.Size = 15;
                worksheet.Cells[5, 1, 5, 4].Style.Font.Bold =true;
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

        //private IActionResult GenerateExcelReportForQuestionnaire(Response response)
        //{
        //    var userName = response.UserName;
        //    var userEmail = response.UserEmail;

        //    using (var package = new ExcelPackage())
        //    {
        //        var worksheet = package.Workbook.Worksheets.Add("Report");

        //        // Add a logo
        //        var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
        //        if (System.IO.File.Exists(logoPath))
        //        {
        //            var logo = new FileInfo(logoPath);
        //            var picture = worksheet.Drawings.AddPicture("Logo", logo);
        //            picture.SetPosition(0, 0, 0, 0);
        //            picture.SetSize(300, 70); // Adjust the size as needed
        //        }

        //        // Add a title
        //        worksheet.Cells[6, 1].Value = $"Report for {response.Questionnaire.Title}";
        //        worksheet.Cells[6, 1, 6, 4].Merge = true;
        //        worksheet.Cells[6, 1, 6, 4].Style.Font.Size = 18;
        //        worksheet.Cells[6, 1, 6, 4].Style.Font.Bold = true;
        //        worksheet.Cells[6, 1, 6, 4].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

        //        // Add headers
        //        worksheet.Cells[7, 1].Value = "Survey";
        //        worksheet.Cells[7, 2].Value = "Submitted on";
        //        worksheet.Cells[7, 3].Value = "Question";
        //        worksheet.Cells[7, 4].Value = "Response";

        //        using (var range = worksheet.Cells[7, 1, 7, 4])
        //        {
        //            range.Style.Font.Bold = true;
        //            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        //            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        //            range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        //        }

        //        // Add data
        //        var row = 8;
        //        worksheet.Cells[row, 1].Value = response.Questionnaire.Title;
        //        worksheet.Cells[row, 2].Value = response.SubmissionDate.ToString();
        //        row++;

        //        foreach (var detail in response.ResponseDetails)
        //        {
        //            worksheet.Cells[row, 3].Value = detail.Question.Text;

        //            if (detail.QuestionType == QuestionType.Text || detail.QuestionType == QuestionType.Slider || detail.QuestionType == QuestionType.Open_ended)
        //            {
        //                worksheet.Cells[row, 4].Value = detail.TextResponse;
        //            }
        //            else
        //            {
        //                var answers = string.Join(", ", detail.ResponseAnswers.Select(a => detail.Question.Answers.FirstOrDefault(ans => ans.Id == a.AnswerId)?.Text));
        //                worksheet.Cells[row, 4].Value = answers;
        //            }
        //            row++;
        //        }

        //        worksheet.Cells.AutoFitColumns();

        //        var stream = new MemoryStream();
        //        package.SaveAs(stream);
        //        stream.Position = 0;

        //        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{response.Questionnaire.Title}_{userEmail}.xlsx");
        //    }
        //}




    }
}
