using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.IdentityModel.Tokens;
using Model;

using Services.Interaces;
using System.Security.Cryptography;
using Web.ViewModel.QuestionnaireVM;


namespace Web.Areas.Admin.Controllers
{
    public class QuestionnaireController : Controller
    {
        private readonly IQuestionnaireRepository _questionnaire;
        private readonly SurveyContext _context;
        private readonly IQuestionRepository _question;

        public QuestionnaireController(IQuestionnaireRepository Questionnaire, SurveyContext Context, IQuestionRepository Question)
        {
            _questionnaire = Questionnaire;
            _context = Context;
            _question = Question;
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

                    if(questionViewModel.Id !=0)
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

    }
}
