using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Model;
using Newtonsoft.Json;
using Services.Interaces;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Web.ViewModel.AnswerVM;
using Web.ViewModel.QuestionnaireVM;
using Web.ViewModel.QuestionVM;

namespace Web.Controllers
{
    public class QuestionnaireResponseController : Controller
    {
        private readonly IQuestionnaireRepository _questionnaireRepository;
        private readonly SurveyContext _context;

        public QuestionnaireResponseController(IQuestionnaireRepository questionnaireRepository,SurveyContext context)
        {
            _questionnaireRepository = questionnaireRepository;
            _context = context;
        }
        public IActionResult Index()
        {
          
            return View();
        }

        public IActionResult Error()
        {
            ViewBag.ErrorMessage = "The survey link has expired. request a new link.";

            return View();
        }

        public IActionResult DisplayQuestionnaire(int id,string t)
        {
            // Check if the token is null or empty
            if (string.IsNullOrEmpty(t))
            {
                ViewBag.ErrorMessage = "The URL is invalid. Please provide a valid token.";
                return View("Error");
            }

            // Split the token to extract the expiration date and time
            string[] tokenParts = t.Split('|');
            if (tokenParts.Length != 2)
            {
                ViewBag.ErrorMessage = "The URL is invalid. Please provide a valid token.";
                return View("Error");
            }

            string expiryDateTimeString = tokenParts[1];

            // Parse the expiration datetime in UTC format
            if (!DateTime.TryParseExact(expiryDateTimeString, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime expiryDateTimeUtc))
            {
                ViewBag.ErrorMessage = "The URL is invalid. Please provide a valid token.";
                return View("Error");
            }

            // Convert the expiration datetime to local time
            DateTime expiryDateTimeLocal = expiryDateTimeUtc.ToLocalTime();

            // Check if the token is expired (accounting for UTC+2 offset)
            if (expiryDateTimeLocal < DateTime.Now.AddHours(2))
            {

                return RedirectToAction(nameof(Error));
            }

            // Retrieve the questionnaire using the numeric ID
            var questionnaire = _questionnaireRepository.GetQuestionnaireWithQuestionAndAnswer(id);

            return View(MapToViewModel(questionnaire));
        }
        [HttpPost]
        public IActionResult DisplayQuestionnaire([FromForm] ResponseQuestionnaireViewModel questionnaire)
        {
            //bool hasSubmitted = _context.Responses.Any(r => r.QuestionnaireId == questionnaire.Id && r.UserEmail == questionnaire.Email);

            //if (hasSubmitted)
            //{
            //    TempData["ErrorMessage"] = "You have already completed this survey.";
            //    return RedirectToAction("ThankYou");
            //}

            var response = new Response
            {
                QuestionnaireId = questionnaire.Id,
                UserName = questionnaire.UserName,
                UserEmail = questionnaire.Email,
                SubmissionDate = DateTime.UtcNow,
                ResponseDetails = questionnaire.Questions.Select(q => new ResponseDetail
                {
                    QuestionId = q.Id,
                    QuestionType=q.Type,
                    // Handle TextResponse based on question type
                    TextResponse = (q.Type == QuestionType.Open_ended || q.Type == QuestionType.Text || q.Type==QuestionType.Slider)
                                   ? string.Join(" ", q.SelectedText)  // Ensure SelectedText is appropriately used based on question type
                                   : null,
                    ResponseAnswers = q.SelectedAnswerIds
                        .Select(aid => new ResponseAnswer { AnswerId = aid })
                        .ToList() // Ensure that the list is initialized correctly
                }).ToList()
            };


            _context.Responses.Add(response);
            _context.SaveChanges();

            TempData["UserName"] = questionnaire.UserName;

            return RedirectToAction(nameof(ThankYou));

        }

        [HttpGet]
        public IActionResult ThankYou()
        {
            ViewBag.UserName = TempData["UserName"];
            return View();
        }


        private ResponseQuestionnaireViewModel MapToViewModel(Questionnaire questionnaire)
        {
            var viewModel = new ResponseQuestionnaireViewModel
            {
                Id = questionnaire.Id,
                Title = questionnaire.Title,
                Description = questionnaire.Description,
                Questions = questionnaire.Questions.Select(q => new ResponseQuestionViewModel
                {
                    Id = q.Id,
                    Text = q.Text,
                    Type = q.Type,
                    Answers = q.Answers.Select(a => new ResponseAnswerViewModel
                    {
                        Id = a.Id,
                        Text = a.Text
                    }).ToList()
                }).ToList()
            };

            return viewModel;
        }




    }
}








