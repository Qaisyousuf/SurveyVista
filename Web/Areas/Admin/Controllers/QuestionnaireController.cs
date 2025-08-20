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
        public async Task<IActionResult> Index()
        {
            var questionnaire = _questionnaire.GetAllQuestionnairesWithStatus(); // Use new method
            var question = _question.GetQuestionsWithAnswers(); // Keep your existing line

            List<QuestionnaireViewModel> viewmodel = new List<QuestionnaireViewModel>();

            foreach (var item in questionnaire)
            {
                // Check if this questionnaire has responses
                var hasResponses = await _questionnaire.HasResponses(item.Id);

                viewmodel.Add(new QuestionnaireViewModel
                {
                    // EXISTING MAPPING (keep exactly as-is):
                    Id = item.Id,
                    Description = item.Description,
                    Title = item.Title,
                    Questions = item.Questions,

                    // ADD NEW STATUS MAPPING:
                    Status = item.Status,
                    CreatedDate = item.CreatedDate,
                    PublishedDate = item.PublishedDate,
                    ArchivedDate = item.ArchivedDate,
                    HasResponses = hasResponses
                });
            }

            return View(viewmodel);
        }

        [HttpGet]
        public IActionResult StatusGuide()
        {
            // Simple documentation page - no model needed
            return View();
        }
        // Add this to your controller
        public async Task<JsonResult> GetQuestionnaireStats(int id)
        {
            var responseCount = await _context.Responses
                .CountAsync(r => r.QuestionnaireId == id);

            var questionCount = await _context.Questions
                .CountAsync(q => q.QuestionnaireId == id && q.IsActive);

            var questionnaire = await _context.Questionnaires.FindAsync(id);

            return Json(new
            {
                responseCount,
                questionCount,
                status = questionnaire?.Status.ToString(),
                hasResponses = responseCount > 0
            });
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
                        // Skip empty answers
                        if (string.IsNullOrWhiteSpace(answerViewModel.Text))
                            continue;

                        var answer = new Answer
                        {
                            Text = answerViewModel.Text,
                            QuestionId = answerViewModel.QuestionId,
                            IsOtherOption = answerViewModel.IsOtherOption // NEW: Handle IsOtherOption property
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
        public IActionResult Edit(int id)
        {
            var questionTypes = Enum.GetValues(typeof(QuestionType))
                .Cast<QuestionType>()
                .Select(e => new SelectListItem { Value = e.ToString(), Text = e.ToString() });
            ViewBag.QuestionTypes = questionTypes;

            var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(id);

            if (questionnaire == null)
            {
                return NotFound();
            }

            // ADD THIS LINE: Pass questionnaire to view for status checking
            ViewBag.Questionnaire = questionnaire;

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
                            QuestionId = a.QuestionId,
                            IsOtherOption = a.IsOtherOption
                        }).ToList()
                    }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EditQuestionnaireViewModel viewModel)
        {
            Console.WriteLine("=== STATUS-AWARE EDIT POST METHOD CALLED ===");
            Console.WriteLine($"Questionnaire ID: {viewModel?.Id}");
            Console.WriteLine($"Questions count: {viewModel?.Questions?.Count ?? 0}");

            var questionTypes = Enum.GetValues(typeof(QuestionType))
                .Cast<QuestionType>()
                .Select(e => new SelectListItem { Value = e.ToString(), Text = e.ToString() });
            ViewBag.QuestionTypes = questionTypes;

            if (ModelState.IsValid)
            {
                try
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    Console.WriteLine("Database transaction started");

                    try
                    {
                        // Step 1: Get the questionnaire with its current status
                        var existingQuestionnaire = await _context.Questionnaires
                            .FirstOrDefaultAsync(q => q.Id == viewModel.Id);

                        if (existingQuestionnaire == null)
                        {
                            return NotFound();
                        }

                        Console.WriteLine($"Questionnaire Status: {existingQuestionnaire.Status}");

                        // Step 2: Check if questionnaire can be edited
                        if (existingQuestionnaire.Status == QuestionnaireStatus.Archived)
                        {
                            TempData["Error"] = "Archived questionnaires cannot be edited. Please revert to draft status first.";
                            await transaction.RollbackAsync();
                            return RedirectToAction(nameof(Index));
                        }

                        // Step 3: Update basic questionnaire info (always allowed)
                        existingQuestionnaire.Title = viewModel.Title;
                        existingQuestionnaire.Description = viewModel.Description;

                        // Step 4: Get existing questions
                        var existingQuestions = await _context.Questions
                            .Include(q => q.Answers)
                            .Where(q => q.QuestionnaireId == viewModel.Id && q.IsActive)
                            .ToListAsync();

                        Console.WriteLine($"Found {existingQuestions.Count} existing active questions");

                        // Step 5: Handle editing based on questionnaire status
                        switch (existingQuestionnaire.Status)
                        {
                            case QuestionnaireStatus.Draft:
                                Console.WriteLine("DRAFT MODE: Full editing allowed");
                                await HandleDraftQuestionnaire(viewModel, existingQuestions, existingQuestionnaire.Id);
                                break;

                            case QuestionnaireStatus.Published:
                                Console.WriteLine("PUBLISHED MODE: Limited editing (preserves response data)");
                                await HandlePublishedQuestionnaire(viewModel, existingQuestions, existingQuestionnaire.Id);
                                break;
                        }

                        // Step 6: Final save and commit
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        Console.WriteLine("Transaction committed successfully");

                        // Step 7: Success message
                        var finalQuestionCount = await _context.Questions
                            .CountAsync(q => q.QuestionnaireId == viewModel.Id && q.IsActive);

                        TempData["Success"] = $"Questionnaire updated successfully with {finalQuestionCount} question(s)!";

                        if (existingQuestionnaire.Status == QuestionnaireStatus.Published)
                        {
                            TempData["Info"] = "Limited editing was applied to preserve response data integrity.";
                        }

                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR in transaction: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR in Edit method: {ex.Message}");
                    ModelState.AddModelError("", $"An error occurred while updating the questionnaire: {ex.Message}");
                    return View(viewModel);
                }
            }
            else
            {
                Console.WriteLine("ModelState is NOT valid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Validation error: {error.ErrorMessage}");
                }
            }

            return View(viewModel);
        }

        // Handle Draft Questionnaires (Full Editing Allowed)
        private async Task HandleDraftQuestionnaire(EditQuestionnaireViewModel viewModel,
            List<Question> existingQuestions, int questionnaireId)
        {
            Console.WriteLine("Processing DRAFT questionnaire - full editing allowed");

            var incomingQuestionIds = viewModel.Questions?
                .Where(q => q.Id > 0 && !string.IsNullOrWhiteSpace(q.Text))
                .Select(q => q.Id)
                .ToList() ?? new List<int>();

            // HARD DELETE is safe for draft questionnaires (no responses exist)
            var questionsToDelete = existingQuestions
                .Where(eq => !incomingQuestionIds.Contains(eq.Id))
                .ToList();

            if (questionsToDelete.Any())
            {
                Console.WriteLine($"Hard deleting {questionsToDelete.Count} questions from draft");

                // Delete answers first
                var questionIdsToDelete = questionsToDelete.Select(q => q.Id).ToList();
                var answersToDelete = await _context.Answers
                    .Where(a => questionIdsToDelete.Contains(a.QuestionId))
                    .ToListAsync();

                _context.Answers.RemoveRange(answersToDelete);
                await _context.SaveChangesAsync();

                // Delete questions
                _context.Questions.RemoveRange(questionsToDelete);
                await _context.SaveChangesAsync();
            }

            // Process remaining questions (full editing allowed)
            await ProcessQuestions(viewModel, existingQuestions, questionnaireId, allowFullEditing: true);
        }

        // Handle Published Questionnaires (Limited Editing)
        private async Task HandlePublishedQuestionnaire(EditQuestionnaireViewModel viewModel,
            List<Question> existingQuestions, int questionnaireId)
        {
            Console.WriteLine("Processing PUBLISHED questionnaire - limited editing");

            var incomingQuestionIds = viewModel.Questions?
                .Where(q => q.Id > 0 && !string.IsNullOrWhiteSpace(q.Text))
                .Select(q => q.Id)
                .ToList() ?? new List<int>();

            // SOFT DELETE for published questionnaires (preserve response relationships)
            var questionsToSoftDelete = existingQuestions
                .Where(eq => !incomingQuestionIds.Contains(eq.Id))
                .ToList();

            foreach (var question in questionsToSoftDelete)
            {
                question.IsActive = false;
                Console.WriteLine($"Soft deleted question: {question.Text}");
            }

            // Process remaining questions (limited editing)
            await ProcessQuestions(viewModel, existingQuestions, questionnaireId, allowFullEditing: false);
        }

        // Common Question Processing Logic
        private async Task ProcessQuestions(EditQuestionnaireViewModel viewModel,
            List<Question> existingQuestions, int questionnaireId, bool allowFullEditing)
        {
            if (viewModel.Questions == null) return;

            var validQuestions = viewModel.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q.Text))
                .ToList();

            Console.WriteLine($"Processing {validQuestions.Count} valid questions");

            foreach (var questionViewModel in validQuestions)
            {
                if (questionViewModel.Id > 0)
                {
                    // UPDATE existing question
                    var existingQuestion = existingQuestions.FirstOrDefault(eq => eq.Id == questionViewModel.Id && eq.IsActive);
                    if (existingQuestion != null)
                    {
                        Console.WriteLine($"Updating question {existingQuestion.Id}: '{questionViewModel.Text}'");

                        // Always allow text and type updates
                        existingQuestion.Text = questionViewModel.Text.Trim();
                        existingQuestion.Type = questionViewModel.Type;

                        // Answer editing depends on questionnaire status and response data
                        if (allowFullEditing)
                        {
                            // Full answer editing (Draft mode)
                            Console.WriteLine($"Full answer editing for question {existingQuestion.Id}");
                            await UpdateQuestionAnswers(existingQuestion, questionViewModel);
                        }
                        else
                        {
                            // Limited answer editing (Published mode) - only if no responses
                            var hasResponses = await _context.ResponseDetails
                                .AnyAsync(rd => rd.QuestionId == existingQuestion.Id);

                            if (!hasResponses)
                            {
                                Console.WriteLine($"No responses found - updating answers for question {existingQuestion.Id}");
                                await UpdateQuestionAnswers(existingQuestion, questionViewModel);
                            }
                            else
                            {
                                Console.WriteLine($"Question {existingQuestion.Id} has responses - preserving answers");
                                TempData["Warning"] = "Some questions have responses, so their answer options were preserved to maintain data integrity.";
                            }
                        }
                    }
                }
                else
                {
                    // CREATE new question (always allowed)
                    Console.WriteLine($"Creating new question: '{questionViewModel.Text}'");

                    var newQuestion = new Question
                    {
                        Text = questionViewModel.Text.Trim(),
                        Type = questionViewModel.Type,
                        QuestionnaireId = questionnaireId,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.Questions.Add(newQuestion);
                    await _context.SaveChangesAsync(); // Save to get the ID

                    Console.WriteLine($"New question created with ID: {newQuestion.Id}");

                    // Add answers for the new question
                    await AddAnswersToQuestion(newQuestion, questionViewModel);
                }
            }
        }

        // Helper: Update Question Answers
        private async Task UpdateQuestionAnswers(Question question, Question questionViewModel)
        {
            // Remove existing answers
            var existingAnswers = question.Answers.ToList();
            if (existingAnswers.Any())
            {
                _context.Answers.RemoveRange(existingAnswers);
                await _context.SaveChangesAsync();
            }

            // Add new answers
            await AddAnswersToQuestion(question, questionViewModel);
        }

        // Helper: Add Answers to Question
        private async Task AddAnswersToQuestion(Question question, Question questionViewModel)
        {
            if (questionViewModel.Answers != null && questionViewModel.Answers.Any())
            {
                var validAnswers = questionViewModel.Answers
                    .Where(a => !string.IsNullOrWhiteSpace(a.Text))
                    .ToList();

                Console.WriteLine($"Adding {validAnswers.Count} answers to question {question.Id}");

                foreach (var answerViewModel in validAnswers)
                {
                    var newAnswer = new Answer
                    {
                        Text = answerViewModel.Text.Trim(),
                        IsOtherOption = answerViewModel.IsOtherOption,
                        QuestionId = question.Id
                    };
                    _context.Answers.Add(newAnswer);
                }

                if (validAnswers.Any())
                {
                    await _context.SaveChangesAsync();
                }
            }
        }
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var questionTypes = Enum.GetValues(typeof(QuestionType)).Cast<QuestionType>();

            ViewBag.QuestionTypes = new SelectList(questionTypes);
            var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(id);

            if (questionnaire == null)
            {
                return NotFound();
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
                            Text = a.Text,
                            IsOtherOption = a.IsOtherOption // NEW: Include IsOtherOption property
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
                return NotFound();
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
                            Text = a.Text,
                            IsOtherOption = a.IsOtherOption // NEW: Include IsOtherOption property
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
        // Add these methods to your existing controller (the one with SetLogic and SaveLogic)

        [HttpPost]
        public async Task<IActionResult> SaveAnswerCondition([FromBody] SaveAnswerConditionRequest request)
        {
            try
            {
                // Validate the request
                if (request == null || request.AnswerId <= 0)
                {
                    return Json(new { success = false, message = "Invalid answer ID provided." });
                }

                // Get the questionnaire with all related data
                var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(request.QuestionnaireId);

                if (questionnaire == null)
                {
                    return Json(new { success = false, message = "Questionnaire not found." });
                }

                // Find the specific answer
                var answer = questionnaire.Questions
                    .SelectMany(q => q.Answers)
                    .FirstOrDefault(a => a.Id == request.AnswerId);

                if (answer == null)
                {
                    return Json(new { success = false, message = "Answer not found." });
                }

                // Validate and store the condition JSON
                if (string.IsNullOrEmpty(request.ConditionJson))
                {
                    // Clear the condition (set to continue)
                    answer.ConditionJson = null;
                }
                else
                {
                    // Validate JSON format
                    try
                    {
                        var testParse = System.Text.Json.JsonSerializer.Deserialize<ConditionData>(request.ConditionJson);
                        answer.ConditionJson = request.ConditionJson;
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        return Json(new { success = false, message = "Invalid condition data format." });
                    }
                }

                // Save changes using your repository pattern
                await _questionnaire.Update(questionnaire);
                await _questionnaire.commitAsync();

                // Generate summary for response
                string summary = GetConditionSummaryFromJson(answer.ConditionJson);

                return Json(new
                {
                    success = true,
                    message = "Answer condition saved successfully!",
                    summary = summary
                });
            }
            catch (Exception ex)
            {
                // Log error if you have logging configured
                // _logger?.LogError(ex, "Error saving answer condition for AnswerId: {AnswerId}", request.AnswerId);
                return Json(new
                {
                    success = false,
                    message = "An error occurred while saving the condition. Please try again."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResetAnswerCondition([FromBody] ResetAnswerConditionRequest request)
        {
            try
            {
                if (request == null || request.AnswerId <= 0)
                {
                    return Json(new { success = false, message = "Invalid answer ID provided." });
                }

                var questionnaire = _questionnaire.GetQuestionnaireWithQuestionAndAnswer(request.QuestionnaireId);

                if (questionnaire == null)
                {
                    return Json(new { success = false, message = "Questionnaire not found." });
                }

                var answer = questionnaire.Questions
                    .SelectMany(q => q.Answers)
                    .FirstOrDefault(a => a.Id == request.AnswerId);

                if (answer == null)
                {
                    return Json(new { success = false, message = "Answer not found." });
                }

                // Reset to default (Continue)
                answer.ConditionJson = null;

                await _questionnaire.Update(questionnaire);
                await _questionnaire.commitAsync();

                return Json(new
                {
                    success = true,
                    message = "Answer condition reset successfully!"
                });
            }
            catch (Exception ex)
            {
                // _logger?.LogError(ex, "Error resetting answer condition for AnswerId: {AnswerId}", request.AnswerId);
                return Json(new
                {
                    success = false,
                    message = "An error occurred while resetting the condition. Please try again."
                });
            }
        }

        // Helper method to generate summary from JSON
        private string GetConditionSummaryFromJson(string conditionJson)
        {
            if (string.IsNullOrEmpty(conditionJson))
            {
                return "Continue to the next question normally";
            }

            try
            {
                var condition = System.Text.Json.JsonSerializer.Deserialize<ConditionData>(conditionJson);
                if (condition == null) return "Continue to the next question normally";

                switch (condition.ActionType)
                {
                    case 0: // Continue
                        return "Continue to the next question normally";
                    case 1: // SkipToQuestion
                        return condition.TargetQuestionNumber.HasValue
                            ? $"Jump to Question {condition.TargetQuestionNumber}"
                            : "Jump to specific question";
                    case 2: // SkipCount
                        return $"Skip {condition.SkipCount ?? 1} question(s)";
                    case 3: // EndSurvey
                        return "End the survey immediately";
                    default:
                        return "Continue to the next question normally";
                }
            }
            catch
            {
                return "Continue to the next question normally";
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishQuestionnaire(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires.FindAsync(id);
                if (questionnaire == null)
                {
                    TempData["Error"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (questionnaire.Status != QuestionnaireStatus.Draft)
                {
                    TempData["Error"] = "Only draft questionnaires can be published.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if questionnaire has questions
                var hasQuestions = await _context.Questions
                    .AnyAsync(q => q.QuestionnaireId == id && q.IsActive);

                if (!hasQuestions)
                {
                    TempData["Error"] = "Cannot publish questionnaire without questions.";
                    return RedirectToAction(nameof(Index));
                }

                await _questionnaire.UpdateStatus(id, QuestionnaireStatus.Published);
                TempData["Success"] = $"Questionnaire '{questionnaire.Title}' has been published successfully!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing questionnaire {id}: {ex.Message}");
                TempData["Error"] = "An error occurred while publishing the questionnaire.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==================================================
        // NEW METHOD 2: Archive Questionnaire
        // ==================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveQuestionnaire(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires.FindAsync(id);
                if (questionnaire == null)
                {
                    TempData["Error"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (questionnaire.Status != QuestionnaireStatus.Published)
                {
                    TempData["Error"] = "Only published questionnaires can be archived.";
                    return RedirectToAction(nameof(Index));
                }

                await _questionnaire.UpdateStatus(id, QuestionnaireStatus.Archived);
                TempData["Success"] = $"Questionnaire '{questionnaire.Title}' has been archived successfully.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error archiving questionnaire {id}: {ex.Message}");
                TempData["Error"] = "An error occurred while archiving the questionnaire.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==================================================
        // NEW METHOD 3: Revert to Draft
        // ==================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevertToDraft(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires.FindAsync(id);
                if (questionnaire == null)
                {
                    TempData["Error"] = "Questionnaire not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (questionnaire.Status == QuestionnaireStatus.Draft)
                {
                    TempData["Warning"] = "Questionnaire is already in draft status.";
                    return RedirectToAction(nameof(Index));
                }

                var hasResponses = await _questionnaire.HasResponses(id);
                if (hasResponses)
                {
                    TempData["Error"] = "Cannot revert questionnaire to draft status because it has survey responses.";
                    return RedirectToAction(nameof(Index));
                }

                await _questionnaire.UpdateStatus(id, QuestionnaireStatus.Draft);
                TempData["Success"] = $"Questionnaire '{questionnaire.Title}' has been reverted to draft status.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reverting questionnaire {id}: {ex.Message}");
                TempData["Error"] = "An error occurred while reverting the questionnaire.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==================================================
        // NEW METHOD 4: Get Status Info (Helper for Views)
        // ==================================================
        [HttpGet]
        public async Task<IActionResult> GetQuestionnaireStatus(int id)
        {
            try
            {
                var questionnaire = await _context.Questionnaires.FindAsync(id);
                if (questionnaire == null)
                {
                    return Json(new { success = false, message = "Questionnaire not found" });
                }

                var hasResponses = await _questionnaire.HasResponses(id);
                var questionCount = await _context.Questions
                    .CountAsync(q => q.QuestionnaireId == id && q.IsActive);

                return Json(new
                {
                    success = true,
                    status = questionnaire.Status.ToString(),
                    hasResponses = hasResponses,
                    questionCount = questionCount,
                    createdDate = questionnaire.CreatedDate,
                    publishedDate = questionnaire.PublishedDate,
                    archivedDate = questionnaire.ArchivedDate
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting status for questionnaire {id}: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving status" });
            }
        }


        // Request models - Add these classes to your project
        public class SaveAnswerConditionRequest
        {
            public int AnswerId { get; set; }
            public string ConditionJson { get; set; } = string.Empty;
            public int QuestionnaireId { get; set; }
        }

        public class ResetAnswerConditionRequest
        {
            public int AnswerId { get; set; }
            public int QuestionnaireId { get; set; }
        }

        public class ConditionData
        {
            public int ActionType { get; set; }
            public int? TargetQuestionNumber { get; set; }
            public int? SkipCount { get; set; }
            public string? EndMessage { get; set; }
        }

    }


}
