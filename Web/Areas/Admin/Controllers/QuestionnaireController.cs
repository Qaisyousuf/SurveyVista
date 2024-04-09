﻿using Data;
using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Elfie.Extensions;
using Microsoft.IdentityModel.Tokens;
using Model;
using Services.EmailSend;
using Services.Interaces;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
            catch (Exception ex)
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
        public async Task<IActionResult> SendQuestionnaire(SendQuestionnaireViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
              

                // Build the email body with questionnaire details
                var questionnairePath = _configuration["Email:Questionnaire"];
                int surveyId = viewModel.QuestionnaireId;

                //DateTime currentDateTime = DateTime.Now;
                //DateTime currentDateTime = viewModel.ExpirationDateTime;
                DateTime currentDateTime;
                if (viewModel.ExpirationDateTime.HasValue)
                {
                    currentDateTime = viewModel.ExpirationDateTime.Value;
                }
                else
                {
                    // Handle the case when ExpirationDateTime is null
                    // For example, you can assign the current date and time
                    currentDateTime = DateTime.Now;
                }

                // Calculate the expiration date and time by adding 5 minutes to the current date and time
                DateTime expiryDateTime = currentDateTime;

                // Generate a unique token, for example, using a cryptographic library or a GUID
                string token = Guid.NewGuid().ToString();

                // Append the expiration date and time to the token (you might want to encrypt it for security)
                string tokenWithExpiry = $"{token}|{expiryDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")}";



                var completeUrl = $"{Request.Scheme}://{Request.Host}/{questionnairePath}/{viewModel.QuestionnaireId}?token={tokenWithExpiry}";

                //var completeUrl = $"{Request.Scheme}://{Request.Host}/{questionnairePath}/{viewModel.QuestionnaireId}";


                var toEmail = viewModel.Email;
                var question = _questionnaire.GetQuesById(viewModel.Id);

                var subject = question.Title;

                // Construct the email body with HTML formatting
                string emailBody = $@"
                                        <html>
                                        <head>
                                            <style>
                                                /* Inline CSS styles */
                                                body {{
                                                    font-family: Arial, sans-serif;
                                                }}
                                                .text-danger {{
                                                  color:red;  
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
                                                <h4>Hey {viewModel.Name},</h4>
                                                <h5>{subject}</h5>
                                             <p>Thank you for participating in our survey. Your feedback is valuable to us.</p>
                                               <p>Please click the button below to start the survey:</p>
                                                    <p class=""text-danger"">The survey will be expire in Date:{expiryDateTime.ToLongDateString()} Time: {expiryDateTime.ToShortTimeString()} </p>
                                                     <div style='text-align: center;'>
                                                    <a href='{completeUrl}' class='button'>Start Survey</a>
                                                <br>
                                               
                                                </div><br>
                                                <p><strong>Søren Eggert Lundsteen Olsen</strong>
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

                bool emailSent = await _emailServices.SendConfirmationEmailAsync(emailSend);

                if (emailSent)
                {
                    // Email sent successfully
                    // You can redirect to a success page or return a success message
                    TempData["Success"] = "Questionnaire sent successfully";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    // Email failed to send
                    // You can return an error message or handle it as needed
                    ModelState.AddModelError(string.Empty, "Failed to send questionnaire via email.");
                }
            }

            // If model state is not valid, return the view with validation errors
            return View(viewModel);
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
