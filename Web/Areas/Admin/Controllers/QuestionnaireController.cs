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
                // Your Delete method is async Task, so you need to await it
                // It doesn't return anything, so don't assign it to a variable
                await _questionnaire.Delete(id);

                // If we reach here, deletion was successful (no exception thrown)
                return Json(new { success = true, message = "Item deleted successfully" });
            }
            catch (ArgumentNullException ex)
            {
                return Json(new { success = false, message = "Invalid ID provided" });
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = "Questionnaire not found" });
            }
            catch (Exception ex)
            {
                // Log the actual exception to see what's wrong
                System.Diagnostics.Debug.WriteLine($"Delete error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the questionnaire" });
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
                        { "Importance", "High" },
                        { "List-Unsubscribe", "<mailto:kontakt@nvkn.dk?subject=Unsubscribe>" },
                        { "List-Unsubscribe-Post", "List-Unsubscribe=One-Click" },
                        { "X-Microsoft-Classification", "Personal" }
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


        // ✅ Replace your GenerateEmailBody method with this less promotional version:

        // ✅ Replace your GenerateEmailBody with this VERY simple version:

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
    <title>Spørgeskema</title>
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
        
        .link {{
            color: #0066cc;
            text-decoration: underline;
        }}
        
        .footer {{
            border-top: 1px solid #ccc;
            padding-top: 15px;
            margin-top: 20px;
            font-size: 12px;
            color: #666;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <strong>Nærværskonsulenterne ApS</strong>
        </div>
        
        <div class='content'>
            <p>Hej {userName},</p>
            
            <p>Vi håber du har det godt.</p>
            
            <p>Vi gennemfører en kort undersøgelse om arbejdsmiljø og trivsel på arbejdspladsen. Din erfaring og feedback er meget værdifuld for os.</p>
            
            <p>Undersøgelsen tager kun 3-5 minutter at besvare:</p>
            
            <p><a href='{url}' class='link'>{url}</a></p>
            
            <p><strong>Vigtigt:</strong> Undersøgelsen skal besvares inden {expiryDate} kl. {expiryTime}</p>
            
            <p>Alle svar behandles fortroligt og anonymt.</p>
            
            <p>Hvis du har spørgsmål, er du velkommen til at kontakte os på kontakt@nvkn.dk</p>
            
            <p>På forhånd tak for din tid.</p>
            
            <p>Med venlig hilsen,<br/>
            Nærværskonsulenterne ApS</p>
        </div>
        
        <div class='footer'>
            <p>Nærværskonsulenterne ApS<br/>
            Brødemosevej 24A, 3300 Frederiksværk<br/>
            kontakt@nvkn.dk</p>
            
            <p><a href='mailto:kontakt@nvkn.dk?subject=Afmeld' style='color: #666;'>Klik her for at afmelde emails</a></p>
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

        // Add these methods to your existing QuestionnaireController class

        [HttpGet]
        public IActionResult SetLogic(int id)
        {
            var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(id);

            if (questionnaire == null)
            {
                return NotFound();
            }

            var viewModel = new SetLogicViewModel
            {
                QuestionnaireId = questionnaire.Id,
                QuestionnaireName = questionnaire.Title ?? "Untitled Survey",
                Questions = questionnaire.Questions.Select((q, index) => new QuestionLogicViewModel
                {
                    QuestionId = q.Id,
                    QuestionText = q.Text ?? "",
                    QuestionType = q.Type,
                    QuestionNumber = index + 1,
                    Answers = q.Answers.Select(a =>
                    {
                        var answerCondition = new AnswerConditionViewModel
                        {
                            AnswerId = a.Id,
                            AnswerText = a.Text ?? ""
                        };

                        // Parse existing condition if it exists
                        if (!string.IsNullOrEmpty(a.ConditionJson))
                        {
                            try
                            {
                                var condition = System.Text.Json.JsonSerializer.Deserialize<AnswerConditionViewModel>(a.ConditionJson);
                                if (condition != null)
                                {
                                    answerCondition.ActionType = condition.ActionType;
                                    answerCondition.TargetQuestionNumber = condition.TargetQuestionNumber;
                                    answerCondition.SkipCount = condition.SkipCount;
                                    answerCondition.EndMessage = condition.EndMessage;
                                }
                            }
                            catch (System.Text.Json.JsonException)
                            {
                                // If JSON is malformed, use default values
                            }
                        }

                        return answerCondition;
                    }).ToList()
                }).ToList()
            };

            // Pass total question count for dropdown options
            ViewBag.TotalQuestions = questionnaire.Questions.Count;

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SaveLogic(SaveConditionsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid data provided.";
                return RedirectToAction(nameof(SetLogic), new { id = model.QuestionnaireId });
            }

            try
            {
                // Get questionnaire with answers
                var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(model.QuestionnaireId);

                if (questionnaire == null)
                {
                    TempData["Error"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Update each answer's condition
                foreach (var conditionUpdate in model.Conditions)
                {
                    var answer = questionnaire.Questions
                        .SelectMany(q => q.Answers)
                        .FirstOrDefault(a => a.Id == conditionUpdate.AnswerId);

                    if (answer != null)
                    {
                        answer.ConditionJson = string.IsNullOrEmpty(conditionUpdate.ConditionJson)
                            ? null
                            : conditionUpdate.ConditionJson;
                    }
                }

                // Save changes
                await _questionnaire.Update(questionnaire);
                await _questionnaire.commitAsync();

                TempData["Success"] = "Conditional logic saved successfully!";
                return RedirectToAction(nameof(SetLogic), new { id = model.QuestionnaireId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while saving conditions: " + ex.Message;
                return RedirectToAction(nameof(SetLogic), new { id = model.QuestionnaireId });
            }
        }

    }


}
