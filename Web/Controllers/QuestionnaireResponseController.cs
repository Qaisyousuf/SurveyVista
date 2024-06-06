using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using Model;
using Newtonsoft.Json;
using Services.EmailSend;
using Services.Implemnetation;
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
        private readonly IEmailServices _emailServices;
        private readonly IHubContext<NotificationHub> _hubContext;

        public QuestionnaireResponseController(IQuestionnaireRepository questionnaireRepository,SurveyContext context, IEmailServices emailServices, IHubContext<NotificationHub> hubContext)
        {
            _questionnaireRepository = questionnaireRepository;
            _context = context;
            _emailServices = emailServices;
            _hubContext = hubContext;
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

        public IActionResult DisplayQuestionnaire(int id,string t,string E)
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

            bool hasAlreadyResponded = _context.Responses.Any(r => r.QuestionnaireId == id && r.UserEmail == E);
            if (hasAlreadyResponded)
            {
                // Retrieve the first username associated with the email, if available
                var userName = _context.Responses.Where(x => x.UserEmail == E)
                                                 .Select(x => x.UserName)
                                                 .FirstOrDefault();  // This ensures you get a single result or null

                // Ensure userName is not null or empty to use in the message
                if (!string.IsNullOrEmpty(userName))
                {
                    TempData["ErrorMessage"] = $"{userName}";
                }
                else
                {
                    TempData["ErrorMessage"] = "You have already taken this survey.";
                }

                return RedirectToAction(nameof(SubmittedSurvey));
            }



            // Retrieve the questionnaire using the numeric ID
            var questionnaire = _questionnaireRepository.GetQuestionnaireWithQuestionAndAnswer(id);

            return View(MapToViewModel(questionnaire));
        }
        [HttpPost]
        public IActionResult DisplayQuestionnaire([FromForm] ResponseQuestionnaireViewModel questionnaire)
        {
            bool hasSubmitted = _context.Responses.Any(r => r.QuestionnaireId == questionnaire.Id && r.UserEmail == questionnaire.Email);


            var cetZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            var cetTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cetZone);
            var response = new Response
            {
                QuestionnaireId = questionnaire.Id,
                UserName = questionnaire.UserName,
                UserEmail = questionnaire.Email,
                SubmissionDate = cetTime,
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
            var subject = $"Thank You for Your Feedback, {questionnaire.UserName}!";
            var toEmail = questionnaire.Email;
            string emailBody = $@"
                                        <html>
                                        <head>
                                            <style>
                                                /* Inline CSS styles */
                                                body {{
                                                    font-family: Arial, sans-serif;
                                                }}
                                               
                                                .container {{
                                                    max-width: 600px;
                                                    margin: 0 auto;
                                                    padding: 20px;
                                                    border: 0.5px solid #ccc;
                                                    border-radius: 5px;
                                                    background-color: #f9f9f9;
                                                }}
                                                .button {{
                                                    display: inline-block;
                                                    padding: 10px 20px;
                                                    background-color: #007bff;
                                                    color: #ffffff;
                                                    text-decoration: none;
                                                    border-radius: 4px;
                                                }}
                                                .button:hover {{
                                                    background-color: #0056b3;
                                                }}
                                            </style>
                                        </head>
                                        <body>
                                            <div class='container'>
                                                <h4>Hey {questionnaire.UserName.ToUpper()},</h4>
                                                <h5>{subject}</h5>
                                            <p><strong>Thank you so much for taking the time to provide us with your valuable feedback!</strong></p>

                                                    <p>If you have any more thoughts to share or need assistance, please don't hesitate to reach out. You can email us directly at seo@seosoft.dk, and we'll be more than happy to help.</p>

                                                    <p>Thank you once again, {questionnaire.UserName}, for helping us make SeoSoft ApS even better. We truly value your support and participation.</p>
                                               
                                               
                                              <br>
                                                <p><strong>Søren Eggert Lundsteen Olsen</strong><br>
                                                Seosoft ApS<br>
                                                <hr>
                                                Hovedgaden 3
                                                Jordrup<br>
                                                Kolding 6064<br>
                                                Denmark</p>

                                            </div>
                                        </body>
                                        </html>";


            // Call the SendConfirmationEmailAsync method to send the email
            var emailSend = new EmailToSend(toEmail, subject, emailBody);

            _emailServices.SendConfirmationEmailAsync(emailSend);


            TempData["UserName"] = questionnaire.UserName;

            _hubContext.Clients.All.SendAsync("ReceiveNotification", questionnaire.UserName, questionnaire.Email);

            return RedirectToAction(nameof(ThankYou));

        }

        [HttpGet]
        public IActionResult ThankYou()
        {
            ViewBag.UserName = TempData["UserName"];
            return View();
        }

        [HttpGet]
        public IActionResult SubmittedSurvey()
        {
            ViewBag.submitedEmail = TempData["ErrorMessage"];
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








