using Azure;
using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Model;
using Web.ViewModel.QuestionnaireVM;

using System.IO;

namespace Web.Areas.Admin.Controllers
{
    public class SurveyAnalysisController : Controller
    {
        private readonly SurveyContext _context;

        public SurveyAnalysisController(SurveyContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            var questionnaires = _context.Responses
                               .Include(r => r.Questionnaire)  // Ensure the navigation property is correctly set up in the Response model
                               .Select(r => r.Questionnaire)
                               .Distinct()  // Ensure each questionnaire is listed once
                               .ToList();

            var viewModel = questionnaires.Select(q => new ResponseQuestionnaireWithUsersViewModel
            {
                Id = q.Id,
                Title = q.Title
                
            }).ToList();

            return View(viewModel);
        }


        [HttpGet]
        public IActionResult Analysis(int id)
        {
            var viewModel = _context.Responses
       .Where(r => r.QuestionnaireId == id)
       .Include(r => r.Questionnaire)
           .ThenInclude(q => q.Questions)
               .ThenInclude(q => q.Answers)
       .Select(r => new ResponseQuestionnaireWithUsersViewModel
       {
           Id = r.Questionnaire.Id,
           Title = r.Questionnaire.Title,
           Description = r.Questionnaire.Description,
           UserName = r.UserName, // Assuming you want the user who answered the questionnaire
           Email = r.UserEmail,
           ParticipantCount = _context.Responses.Count(rs => rs.QuestionnaireId == id),
           QuestionsAnsweredPercentage = _context.Questionnaires
                .Where(q => q.Id == id)
                .SelectMany(q => q.Questions)
                .Count() > 0
                ? (double)_context.ResponseDetails
                    .Where(rd => rd.Response.QuestionnaireId == id && rd.TextResponse != null)
                    .Select(rd => rd.QuestionId)
                    .Distinct()
                    .Count() / _context.Questionnaires
                        .Where(q => q.Id == id)
                        .SelectMany(q => q.Questions)
                        .Count() * 100.0
                : 0.0, // Avoid division by zero
           Questions = r.Questionnaire.Questions.Select(q => new ResponseQuestionViewModel
           {
               Id = q.Id,
               Text = q.Text,
               Type = q.Type,
               Answers = q.Answers.Select(a => new ResponseAnswerViewModel
               {
                   Id = a.Id,
                   Text = a.Text ?? _context.ResponseDetails
                        .Where(rd => rd.QuestionId == q.Id && rd.ResponseId == r.Id)
                        .Select(rd => rd.TextResponse)
                        .FirstOrDefault(),
                   Count = _context.ResponseAnswers.Count(ra => ra.AnswerId == a.Id) // Count how many times each answer was selected
               }).ToList(),
               SelectedAnswerIds = _context.ResponseDetails
                   .Where(rd => rd.QuestionId == q.Id)
                   .SelectMany(rd => rd.ResponseAnswers)
                   .Select(ra => ra.AnswerId)
                   .Distinct()
                   .ToList(),
               SelectedText = _context.ResponseDetails
                   .Where(rd => rd.QuestionId == q.Id)
                   .Select(rd => rd.TextResponse)
                   .Where(t => !string.IsNullOrEmpty(t))
                   .ToList()
           }).ToList(),
           Users = _context.Responses
               .Where(rs => rs.QuestionnaireId == id)
               .Select(rs => new ResponseUserViewModel
               {
                   UserName = rs.UserName,
                   Email = rs.UserEmail
               }).Distinct().ToList()
       })
       .FirstOrDefault();

            if (viewModel == null)
            {
                return NotFound("No questionnaire found for the given ID.");
            }

            return View(viewModel);
           
        }

        //[HttpGet]
        //public IActionResult GenerateReport(int id)
        //{
        //    var viewModel = GetQuestionnaireData(id);
        //    if (viewModel == null)
        //    {
        //        return NotFound("No questionnaire found with the given ID.");
        //    }

        //    var webReport = new WebReport();

        //    // Load your FastReport report design
        //    webReport.Report.Load(Path.Combine(env.WebRootPath, "Reports", "QuestionnaireReport.frx"));

        //    // Register the data source
        //    webReport.Report.RegisterData(new[] { viewModel }, "Questionnaire");

        //    webReport.Report.Prepare();

        //    using (MemoryStream ms = new MemoryStream())
        //    {
        //        webReport.Report.Export(new FastReport.Export.PdfSimple.PDFSimpleExport(), ms);
        //        return File(ms.ToArray(), "application/pdf", "QuestionnaireReport.pdf");
        //    }
        //}

   



    }
}

