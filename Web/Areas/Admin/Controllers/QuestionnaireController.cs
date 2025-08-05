using Data;
using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Elfie.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Model;
using Services.EmailSend;
using Services.Interaces;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Web.ViewModel.QuestionnaireVM;


namespace Web.Areas.Admin.Controllers
{


   
    public class QuestionnaireController : Controller
    {
        private readonly IQuestionnaireRepository _questionnaire;
        private readonly SurveyContext _context;
        private readonly IQuestionRepository _question;
        private readonly IConfiguration _configuration;
        private readonly IEmailServices _emailServices;

        public QuestionnaireController(IQuestionnaireRepository Questionnaire, SurveyContext Context, IQuestionRepository Question, IConfiguration configuration, IEmailServices emailServices)
        {
            _questionnaire = Questionnaire;
            _context = Context;
            _question = Question;
            _configuration = configuration;
            _emailServices = emailServices;
        }
        public IActionResult Index()
        {

            var questionnaire = _questionnaire.GetQuestionnairesWithQuestion();

            var question = _question.GetQuestionsWithAnswers();



            List<QuestionnaireViewModel> viewmodel = new List<QuestionnaireViewModel>();


            foreach (var item in questionnaire)
            {
                viewmodel.Add(new QuestionnaireViewModel
                {
                    Id = item.Id,
                    Description = item.Description,
                    Title = item.Title,
                    Questions = item.Questions,



                });
            }

            return View(viewmodel);
        }
        [HttpGet]
        public IActionResult Create()


        {

            var questionTypes = Enum.GetValues(typeof(QuestionType)).Cast<QuestionType>();

            ViewBag.QuestionTypes = new SelectList(questionTypes);

            var questionnaire = new QuestionnaireViewModel
            {

                Questions = new List<Question>(),
                Answers = new List<Answer>()

            };




            return View(questionnaire);
        }
        [HttpPost]
        public async Task<IActionResult> Create(QuestionnaireViewModel viewmodel)
        {



            if (ModelState.IsValid)
            {

                var questionnaire = new Questionnaire
                {
                    Id = viewmodel.Id,
                    Title = viewmodel.Title,
                    Description = viewmodel.Description,
                };


                var questions = viewmodel.Questions;

                foreach (var questionViewModel in viewmodel.Questions)
                {
                    var question = new Question
                    {
                        QuestionnaireId = questionViewModel.QuestionnaireId,
                        Text = questionViewModel.Text,
                        Type = questionViewModel.Type,
                        Answers = new List<Answer>()
                    };

                    foreach (var answerViewModel in questionViewModel.Answers)
                    {
                        var answer = new Answer
                        {
                            Text = answerViewModel.Text,
                            QuestionId = answerViewModel.QuestionId,

                        };


                        question.Answers.Add(answer);
                    }


                    questionnaire.Questions.Add(question);
                }




                _questionnaire.Add(questionnaire);
                await _questionnaire.commitAsync();
                TempData["Success"] = "Questionnaire created successfully";



                return RedirectToAction("Index");
            }
            return View(viewmodel);
        }

        [HttpGet]
        public IActionResult Edit(int? id)
        {
            var questionTypes = Enum.GetValues(typeof(QuestionType))
                           .Cast<QuestionType>()
                           .Select(e => new SelectListItem { Value = e.ToString(), Text = e.ToString() });
            ViewBag.QuestionTypes = questionTypes;

            var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(id);

            if (questionnaire == null)
            {
                return NotFound(); // Or handle not found case appropriately
            }

            var viewModel = new EditQuestionnaireViewModel
            {
                Id = questionnaire.Id,
                Title = questionnaire.Title,
                Description = questionnaire.Description,


                Questions = questionnaire.Questions
                    .Select(q => new Question
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Type = q.Type,
                        QuestionnaireId = q.QuestionnaireId,


                        Answers = q.Answers.Select(a => new Answer
                        {
                            Id = a.Id,
                            Text = a.Text,
                            Question = a.Question,
                            QuestionId = a.QuestionId




                        }).ToList()
                    }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EditQuestionnaireViewModel viewModel)
        {

            var questionTypes = Enum.GetValues(typeof(QuestionType))
           .Cast<QuestionType>()
           .Select(e => new SelectListItem { Value = e.ToString(), Text = e.ToString() });
            ViewBag.QuestionTypes = questionTypes;

            if (ModelState.IsValid)
            {
                // Retrieve the existing questionnaire from the database
                var existingQuestionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(viewModel.Id);

                if (existingQuestionnaire == null)
                {
                    return NotFound(); // Or handle not found case appropriately
                }

                // Update the existing questionnaire with the data from the view model
                existingQuestionnaire.Title = viewModel.Title;
                existingQuestionnaire.Description = viewModel.Description;

                var existingQuestionIds = existingQuestionnaire.Questions.Select(q => q.Id).ToList();

                // Iterate through existing questions and remove those not found in the view model
                foreach (var existingQuestion in existingQuestionnaire.Questions.ToList())
                {
                    // If the ID of the existing question is not found in the view model, remove it
                    if (!viewModel.Questions.Any(q => q.Id == existingQuestion.Id))
                    {
                        existingQuestionnaire.Questions.Remove(existingQuestion);

                    }
                    await _questionnaire.Update(existingQuestionnaire);
                }

                // Update the questionnaire with the modified list of questions






                var newQuestions = new List<Question>();

                // Update or add new questions
                foreach (var questionViewModel in viewModel.Questions)
                {
                    var existingQuestion = existingQuestionnaire.Questions.FirstOrDefault(q => q.Id == questionViewModel.Id);

                    if (questionViewModel.Id != 0)
                    {
                        if (existingQuestion != null)
                        {
                            var answersToRemove = new List<Answer>();
                            existingQuestion.Text = questionViewModel.Text;
                            existingQuestion.Type = questionViewModel.Type;


                            foreach (var answerViewModel in questionViewModel.Answers)
                            {
                                // Check if the answer already exists
                                var existingAnswer = existingQuestion.Answers.FirstOrDefault(a => a.Id == answerViewModel.Id);

                                if (answerViewModel.Id == 0)
                                {

                                    existingQuestion.Answers.Add(new Answer { Text = answerViewModel.Text });


                                }
                                else if (answerViewModel.Text == null)
                                {
                                    existingQuestion.Answers.Remove(existingAnswer);
                                    await _questionnaire.Update(existingQuestionnaire);
                                }


                                else if (existingAnswer != null)
                                {

                                    existingAnswer.Text = answerViewModel.Text;
                                }

                            }



                        }
                    }


                    else
                    {
                        // Create a new question
                        var newQuestion = new Question
                        {
                            Text = questionViewModel.Text,
                            Type = questionViewModel.Type,
                            Answers = new List<Answer>()
                        };

                        foreach (var answerViewModel in questionViewModel.Answers)
                        {
                            if (!string.IsNullOrEmpty(answerViewModel.Text))
                            {
                                // Add new answer if text is not null or empty
                                newQuestion.Answers.Add(new Answer { Text = answerViewModel.Text });
                            }
                        }

                        // Add new question to the list of new questions
                        newQuestions.Add(newQuestion);
                    }
                    existingQuestionnaire.Questions.AddRange(newQuestions);
                    //else
                    //{
                    //    // Add new question
                    //    var newQuestion = new Question
                    //    {
                    //        Text = questionViewModel.Text, // Make sure question text is not null
                    //        Type = questionViewModel.Type, // Make sure question type is not null
                    //        Answers = new List<Answer>() // Initialize answers list
                    //    };

                    //    foreach (var answerViewModel in questionViewModel.Answers)
                    //    {
                    //        // Add new answer
                    //        newQuestion.Answers.Add(new Answer { Text = answerViewModel.Text });
                    //    }

                    //    // Add new question to questionnaire
                    //    existingQuestionnaire.Questions.Add(newQuestion);
                    //}

                }


                await _questionnaire.Update(existingQuestionnaire);

                TempData["Success"] = "Questionnaire updated successfully";
                return RedirectToAction(nameof(Index));
            }

            // If ModelState is not valid, re-display the form with validation errors
            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {

            var questionTypes = Enum.GetValues(typeof(QuestionType)).Cast<QuestionType>();

            ViewBag.QuestionTypes = new SelectList(questionTypes);
            var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(id);

            if (questionnaire == null)
            {
                return NotFound(); // Or handle not found case appropriately
            }

            var viewModel = new QuestionnaireViewModel
            {
                Id = questionnaire.Id,
                Title = questionnaire.Title,
                Description = questionnaire.Description,
                Questions = questionnaire.Questions
                    .Select(q => new Question
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Type = q.Type,
                        Answers = q.Answers.Select(a => new Answer
                        {
                            Id = a.Id,
                            Text = a.Text
                        }).ToList()
                    }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirm(int id)
        {



            try
            {
                var deletedQuestionnaire = _questionnaire.Delete(id);


                if (deletedQuestionnaire == null)
                {
                    return NotFound(); // Or handle not found case appropriately
                }

                // If deletion is successful, you can redirect to a success page or return a success message
                return Json(new { success = true, message = "Item deleted successfully" });
            }
            catch (Exception)
            {
                // Log the exception or handle it appropriately
                return StatusCode(500, "An error occurred while processing your request.");
            }





        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var questionTypes = Enum.GetValues(typeof(QuestionType)).Cast<QuestionType>();

            ViewBag.QuestionTypes = new SelectList(questionTypes);
            var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(id);

            if (questionnaire == null)
            {
                return NotFound(); // Or handle not found case appropriately
            }

            var viewModel = new QuestionnaireViewModel
            {
                Id = questionnaire.Id,
                Title = questionnaire.Title,
                Description = questionnaire.Description,
                Questions = questionnaire.Questions
                    .Select(q => new Question
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Type = q.Type,
                        Answers = q.Answers.Select(a => new Answer
                        {
                            Id = a.Id,
                            Text = a.Text
                        }).ToList()
                    }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult SendQuestionnaire(int id)
        {
            var quesstionnaireFromDb = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(id);
            var sendquestionviewmodel = new SendQuestionnaireViewModel();

            sendquestionviewmodel.QuestionnaireId = id;
            ViewBag.questionnaireName = quesstionnaireFromDb.Title;

            return View(sendquestionviewmodel);

        }


        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> SendQuestionnaire(SendQuestionnaireViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return View(viewModel);

            var questionnairePath = _configuration["Email:Questionnaire"];
            var subject = _questionnaire.GetQuesById(viewModel.Id)?.Title ?? "Survey Invitation";

            var currentDateTime = viewModel.ExpirationDateTime ?? DateTime.Now;
            string token = Guid.NewGuid().ToString();
            string tokenWithExpiry = $"{token}|{currentDateTime:yyyy-MM-ddTHH:mm:ssZ}";

            var emailList = viewModel.Emails.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(e => e.Trim())
                                             .ToList();

            bool allEmailsSent = true;

            foreach (var email in emailList)
            {
                string userName = FormatUserNameFromEmail(email);
                string userEmailEncoded = HttpUtility.UrlEncode(email);
                string completeUrl = $"{Request.Scheme}://{Request.Host}/{questionnairePath}/{viewModel.QuestionnaireId}?t={tokenWithExpiry}&E={userEmailEncoded}";

                string emailBody = GenerateEmailBody(userName, subject, completeUrl, currentDateTime);

                var emailSend = new EmailToSend(email, subject, emailBody)
                {
                    Headers = new Dictionary<string, string>
            {
                { "X-Priority", "1" },
                { "Importance", "High" }
            }
                };

                bool emailSent = await _emailServices.SendConfirmationEmailAsync(emailSend);

                if (!emailSent)
                {
                    allEmailsSent = false;
                    ModelState.AddModelError(string.Empty, $"Failed to send questionnaire to: {email}");
                }
            }

            if (allEmailsSent)
            {
                TempData["Success"] = "Questionnaire sent successfully to all recipients.";
                return RedirectToAction(nameof(Index));
            }

            return View(viewModel);
        }
        private string FormatUserNameFromEmail(string email)
        {
            var usernamePart = email.Split('@')[0];
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(usernamePart.Replace('.', ' '));
        }


        private static string GenerateEmailBody(string userName, string subject, string url, DateTime expiry)
        {
            var danishCulture = new CultureInfo("da-DK");
            string expiryDate = expiry.ToString("dd. MMMM yyyy", danishCulture);
            string expiryTime = expiry.ToString("HH:mm", danishCulture);

            return $@"
<!DOCTYPE html>
<html lang='da'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Invitation til undersøgelse</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
            padding: 20px 0;
            margin: 0;
            line-height: 1.6;
            color: #333;
        }}
        
        .email-wrapper {{
            max-width: 650px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 12px;
            overflow: hidden;
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
        }}
        
        .header {{
            background: linear-gradient(135deg, #33b3ae 0%, #141c27 100%);
            color: white;
            padding: 30px;
            text-align: center;
        }}
        
        .logo {{
            max-width: 200px;
            height: auto;
            margin-bottom: 15px;
            filter: brightness(0) invert(1); /* Makes logo white on dark background */
        }}
        
        .header h1 {{
            font-size: 28px;
            font-weight: 600;
            margin: 0;
            text-shadow: 0 2px 4px rgba(0,0,0,0.2);
        }}
        
        .header p {{
            margin: 8px 0 0 0;
            opacity: 0.9;
            font-size: 16px;
        }}
        
        .content {{
            padding: 40px 30px;
        }}
        
        .greeting {{
            font-size: 20px;
            color: #141c27;
            margin-bottom: 20px;
            font-weight: 500;
        }}
        
        .subject-line {{
            background: linear-gradient(135deg, #33b3ae10 0%, #33b3ae20 100%);
            border-left: 4px solid #33b3ae;
            padding: 20px;
            margin: 20px 0;
            border-radius: 8px;
            font-weight: 600;
            font-size: 18px;
            color: #141c27;
        }}
        
        .content p {{
            margin: 16px 0;
            font-size: 16px;
            color: #555;
            line-height: 1.7;
        }}
        
        .cta-section {{
            text-align: center;
            margin: 35px 0;
            padding: 20px;
        }}
        
        .cta-button {{
            display: inline-block;
            background: linear-gradient(135deg, #33b3ae 0%, #2a9d99 100%);
            color: white;
            text-decoration: none;
            padding: 16px 32px;
            border-radius: 50px;
            font-weight: 600;
            font-size: 16px;
            box-shadow: 0 8px 25px rgba(51, 179, 174, 0.3);
            transition: all 0.3s ease;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }}
        
        .cta-button:hover {{
            transform: translateY(-2px);
            box-shadow: 0 12px 35px rgba(51, 179, 174, 0.4);
            text-decoration: none;
            color: white;
        }}
        
        .expiry-notice {{
            background: linear-gradient(135deg, #fff5f5 0%, #fed7d7 30%);
            border: 1px solid #feb2b2;
            border-radius: 8px;
            padding: 20px;
            margin: 25px 0;
            text-align: center;
        }}
        
        .expiry-notice .icon {{
            font-size: 24px;
            margin-bottom: 8px;
            display: block;
        }}
        
        .expiry-notice p {{
            margin: 0;
            color: #c53030;
            font-weight: 600;
            font-size: 16px;
        }}
        
        .footer {{
            background-color: #f8f9fb;
            padding: 30px;
            border-top: 1px solid #e2e8f0;
        }}
        
        .company-info {{
            text-align: center;
            color: #666;
            font-size: 14px;
            line-height: 1.6;
        }}
        
        .company-info h3 {{
            color: #141c27;
            margin-bottom: 15px;
            font-size: 18px;
            font-weight: 600;
        }}
        
        .company-info a {{
            color: #33b3ae;
            text-decoration: none;
            font-weight: 500;
        }}
        
        .company-info a:hover {{
            text-decoration: underline;
        }}
        
        .divider {{
            height: 1px;
            background: linear-gradient(to right, transparent, #e2e8f0, transparent);
            margin: 30px 0;
        }}
        
        /* Responsive Design */
        @media (max-width: 600px) {{
            .email-wrapper {{
                margin: 0 10px;
                border-radius: 8px;
            }}
            
            .header, .content, .footer {{
                padding: 20px;
            }}
            
            .logo {{
                max-width: 150px;
            }}
            
            .header h1 {{
                font-size: 24px;
            }}
            
            .subject-line {{
                padding: 15px;
                font-size: 16px;
            }}
            
            .cta-button {{
                padding: 14px 28px;
                font-size: 15px;
            }}
        }}
    </style>
</head>
<body>
    <div class='email-wrapper'>
        <div class='header'>
            <img src='https://i.ibb.co/F4DcSKm0/Logo-For-Email.png' alt='Nærværskonsulenterne Logo' class='logo' />
            <h3>Nærværskonsulenterne</h1>
            <p>Invitation til undersøgelse</p>
        </div>
        
        <div class='content'>
            <div class='greeting'>Hej {userName}! 👋</div>
            
            <div class='subject-line'>
                {subject}
            </div>
            
            <p>Du inviteres til at deltage i en kort undersøgelse fra <strong>Nærværskonsulenterne</strong> vedrørende trivsel og samarbejde på arbejdspladsen.</p>
            
            <p>Din deltagelse er meget værdifuld for os, og vi sætter stor pris på din tid og feedback. Undersøgelsen tager kun få minutter at gennemføre.</p>
            
            <div class='cta-section'>
                <a href='{url}' class='cta-button'>Start spørgeskema</a>
            </div>
            
            <div class='expiry-notice'>
                <span class='icon'>⏰</span>
                <p>Vigtigt: Skemaet udløber den {expiryDate} kl. {expiryTime}</p>
            </div>
            
            <p>Hvis du har spørgsmål eller brug for hjælp, er du velkommen til at kontakte os.</p>
            
            <p>På forhånd tak for din deltagelse!</p>
        </div>
        
        <div class='divider'></div>
        
        <div class='footer'>
            <div class='company-info'>
                <h3>Nærværskonsulenterne ApS</h3>
                <p>Brødemosevej 24A<br/>
                3300 Frederiksværk<br/>
                Danmark</p>
                <br/>
                <p>📧 E-mail: <a href='mailto:kontakt@nvkn.dk'>kontakt@nvkn.dk</a></p>
            </div>
        </div>
    </div>
</body>
</html>";
        }




        [HttpGet]
        public async Task<IActionResult> ViewResponse(int id) // Pass the response ID
        {
            var response = await _context.Responses
        .Include(r => r.ResponseDetails)
            .ThenInclude(rd => rd.ResponseAnswers)
        .Include(r => r.ResponseDetails)
            .ThenInclude(rd => rd.Question) // Include questions for detailed display
            .ThenInclude(q => q.Answers) // Include all possible answers for each question
        .FirstOrDefaultAsync(r => r.Id == id);  // Find the response by ID


            if (response == null)
            {
                return NotFound(); // If no response is found, return a NotFound result
            }

            return View(response); // Pass the response to the view
        }



        public string GenerateExpiryToken(DateTime expiryDate)
        {
            // Generate a unique token, for example, using a cryptographic library or a GUID
            string token = Guid.NewGuid().ToString();

            // Append the expiration date to the token (you might want to encrypt it for security)
            string tokenWithExpiry = $"{token}|{expiryDate.ToString("yyyy-MM-ddTHH:mm:ssZ")}";

            return tokenWithExpiry;
        }



    }
}
