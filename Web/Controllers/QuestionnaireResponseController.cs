using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Model;
using Newtonsoft.Json;
using Services.EmailSend;
using Services.Implemnetation;
using Services.Interaces;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Web.ViewModel.QuestionnaireVM;


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
            ViewBag.ErrorMessage = "Undersøgelseslinket er udløbet. Anmod om et nyt link.";
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

            // Get the actual questionnaire from database
            var dbQuestionnaire = _questionnaireRepository.GetQuestionnaireWithQuestionAndAnswer(questionnaire.Id);

            var cetZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            var cetTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cetZone);
            var response = new Response
            {
                QuestionnaireId = questionnaire.Id,
                UserName = questionnaire.UserName,
                UserEmail = questionnaire.Email,
                SubmissionDate = cetTime,
                ResponseDetails = CreateResponseDetailsForAllQuestions(dbQuestionnaire, questionnaire.Questions, questionnaire.QuestionsShown, questionnaire.QuestionsSkipped)
            };

            _context.Responses.Add(response);
            _context.SaveChanges();

            // ✅ PERSONAL SUBJECT LINE (like survey invitation)
            var subject = $"Tak for din besvarelse, {questionnaire.UserName}";

            var toEmail = questionnaire.Email;

            // ✅ SIMPLE, PROFESSIONAL EMAIL BODY
            string emailBody = GenerateThankYouEmailBody(questionnaire.UserName);

            // ✅ SAME HEADERS AS SURVEY INVITATION (Primary Inbox Optimized)
            var emailSend = new EmailToSend(toEmail, subject, emailBody)
            {
                Headers = new Dictionary<string, string>
                    {
                        { "X-Priority", "1" },
                        { "Importance", "High" },
                        { "List-Unsubscribe", "<mailto:kontakt@nvkn.dk?subject=Unsubscribe>" },
                        { "List-Unsubscribe-Post", "List-Unsubscribe=One-Click" },
                        { "X-Microsoft-Classification", "Personal" }
                    }
            };

            _emailServices.SendConfirmationEmailAsync(emailSend);

            TempData["UserName"] = questionnaire.UserName;
            _hubContext.Clients.All.SendAsync("ReceiveNotification", questionnaire.UserName, questionnaire.Email);

            return RedirectToAction(nameof(ThankYou));
        }

        private List<ResponseDetail> CreateResponseDetailsForAllQuestions(Questionnaire questionnaire, List<ResponseQuestionViewModel> answeredQuestions, string questionsShownJson, string questionsSkippedJson)
        {
            var responseDetails = new List<ResponseDetail>();

            // Parse tracking data
            List<int> questionsShown = new List<int>();
            List<SkippedQuestionInfo> questionsSkipped = new List<SkippedQuestionInfo>();

            try
            {
                if (!string.IsNullOrEmpty(questionsShownJson))
                {
                    questionsShown = System.Text.Json.JsonSerializer.Deserialize<List<int>>(questionsShownJson) ?? new List<int>();
                }

                if (!string.IsNullOrEmpty(questionsSkippedJson))
                {
                    questionsSkipped = System.Text.Json.JsonSerializer.Deserialize<List<SkippedQuestionInfo>>(questionsSkippedJson) ?? new List<SkippedQuestionInfo>();
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                // Log error if needed
                Console.WriteLine($"Error parsing tracking data: {ex.Message}");
            }

            // Get ALL questions from the questionnaire
            var allQuestions = questionnaire.Questions.OrderBy(q => q.Id).ToList();

            foreach (var dbQuestion in allQuestions)
            {
                var questionNumber = GetQuestionNumber(dbQuestion.Id, allQuestions);
                var answeredQuestion = answeredQuestions.FirstOrDefault(aq => aq.Id == dbQuestion.Id);
                var skippedInfo = questionsSkipped.FirstOrDefault(sq => sq.QuestionNumber == questionNumber);

                ResponseDetail responseDetail;

                if (answeredQuestion != null && HasValidResponse(answeredQuestion))
                {
                    // Question was ANSWERED
                    responseDetail = new ResponseDetail
                    {
                        QuestionId = dbQuestion.Id,
                        QuestionType = dbQuestion.Type,
                        Status = ResponseStatus.Answered,
                        TextResponse = (dbQuestion.Type == QuestionType.Open_ended ||
                                       dbQuestion.Type == QuestionType.Text ||
                                       dbQuestion.Type == QuestionType.Slider)
                                       ? string.Join(" ", answeredQuestion.SelectedText)
                                       : null,
                        ResponseAnswers = answeredQuestion.SelectedAnswerIds
                            .Select(aid => new ResponseAnswer { AnswerId = aid })
                            .ToList()
                    };
                }
                else if (skippedInfo != null)
                {
                    // Question was SKIPPED due to conditional logic
                    responseDetail = new ResponseDetail
                    {
                        QuestionId = dbQuestion.Id,
                        QuestionType = dbQuestion.Type,
                        Status = ResponseStatus.Skipped,
                        SkipReason = skippedInfo.Reason
                    };
                }
                else if (questionsShown.Contains(questionNumber))
                {
                    // Question was SHOWN but left blank
                    responseDetail = new ResponseDetail
                    {
                        QuestionId = dbQuestion.Id,
                        QuestionType = dbQuestion.Type,
                        Status = ResponseStatus.Shown,
                        SkipReason = null
                    };
                }
                else
                {
                    // Fallback - assume shown if no tracking data available
                    responseDetail = new ResponseDetail
                    {
                        QuestionId = dbQuestion.Id,
                        QuestionType = dbQuestion.Type,
                        Status = ResponseStatus.Shown,
                        SkipReason = null
                    };
                }

                responseDetails.Add(responseDetail);
            }

            return responseDetails;
        }

        private int GetQuestionNumber(int questionId, List<Question> allQuestions)
        {
            return allQuestions.FindIndex(q => q.Id == questionId) + 1;
        }

        private bool HasValidResponse(ResponseQuestionViewModel question)
        {
            bool hasTextResponse = question.SelectedText != null &&
                                  question.SelectedText.Any(text => !string.IsNullOrWhiteSpace(text));

            bool hasAnswerResponse = question.SelectedAnswerIds != null &&
                                    question.SelectedAnswerIds.Any();

            return hasTextResponse || hasAnswerResponse;
        }

        // Add this class for JSON deserialization
        public class SkippedQuestionInfo
        {
            [JsonPropertyName("questionNumber")]
            public int QuestionNumber { get; set; }

            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;
        }


        // ✅ COMPLETE CORRECTED METHOD: Danish Thank You Email Body
        private static string GenerateThankYouEmailBody(string userName)
        {
            return $@"
<!DOCTYPE html>
<html lang='da'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Tak for dit svar</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            font-size: 14px;
            line-height: 1.4;
            color: #333;
            background-color: #ffffff;
            margin: 0;
            padding: 0;
        }}
        
        .container {{
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
        }}
        
        .header {{
            border-bottom: 1px solid #ccc;
            padding-bottom: 10px;
            margin-bottom: 20px;
        }}
        
        .content {{
            margin-bottom: 20px;
        }}
        
        .content p {{
            margin: 10px 0;
        }}
        
        .signature {{
            margin-top: 20px;
            padding-top: 15px;
            border-top: 1px solid #ccc;
        }}
        
        .company-info {{
            font-size: 12px;
            color: #666;
            margin-top: 15px;
        }}
        
        .footer {{
            border-top: 1px solid #ccc;
            padding-top: 15px;
            margin-top: 20px;
            font-size: 12px;
            color: #666;
        }}
        
        .link {{
            color: #0066cc;
            text-decoration: none;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <strong>Nærværskonsulenterne ApS</strong>
        </div>
        
               <div class='content'>
            <p>Kære {userName},</p>
    
            <p><strong>Vi har modtaget din besvarelse. Tak for din tid, {userName}.</strong></p>
    
            <p>Har du spørgsmål, er du velkommen til at kontakte os på <a href='mailto:kontakt@nvkn.dk' class='link'>kontakt@nvkn.dk</a>.</p>
        </div>

        
        <div class='signature'>
            <p>Med venlig hilsen,<br/>
            <strong>Nærværskonsulenterne ApS</strong></p>
        </div>
        
        <div class='company-info'>
            <p><strong>Nærværskonsulenterne ApS</strong><br/>
            Brødemosevej 24A<br/>
            3300 Frederiksværk<br/>
            Danmark</p>
            <br/>
           
        </div>
        
        <div class='footer'>
            <p>Ønsker du ikke længere at modtage disse emails? 
            <a href='mailto:kontakt@nvkn.dk?subject=Afmeld%20fra%20emails' style='color: #666;'>Klik her for at afmelde</a></p>
        </div>
    </div>
</body>
</html>";
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


        //private ResponseQuestionnaireViewModel MapToViewModel(Questionnaire questionnaire)
        //{
        //    var viewModel = new ResponseQuestionnaireViewModel
        //    {
        //        Id = questionnaire.Id,
        //        Title = questionnaire.Title,
        //        Description = questionnaire.Description,
        //        Questions = questionnaire.Questions.Select(q => new ResponseQuestionViewModel
        //        {
        //            Id = q.Id,
        //            Text = q.Text,
        //            Type = q.Type,
        //            Answers = q.Answers.Select(a => new ResponseAnswerViewModel
        //            {
        //                Id = a.Id,
        //                Text = a.Text
        //            }).ToList()
        //        }).ToList()
        //    };

        //    return viewModel;
        //}
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
                        Text = a.Text,
                        ConditionJson = a.ConditionJson  // Add this line
                    }).ToList()
                }).ToList()
            };

            return viewModel;
        }



    }
}








