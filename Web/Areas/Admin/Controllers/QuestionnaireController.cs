using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        public QuestionnaireController(IQuestionnaireRepository Questionnaire,SurveyContext Context, IQuestionRepository Question)
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
                    Id=viewmodel.Id,
                    Title=viewmodel.Title,
                    Description=viewmodel.Description,
                };

                
                var questions = viewmodel.Questions;

                foreach (var questionViewModel in viewmodel.Questions)
                {
                    var question = new Question
                    {
                        QuestionnaireId=questionViewModel.QuestionnaireId,
                        Text = questionViewModel.Text,
                        Type = questionViewModel.Type,
                        Answers = new List<Answer>() 
                    };

                    foreach (var answerViewModel in questionViewModel.Answers)
                    {
                        var answer = new Answer
                        {
                            Text = answerViewModel.Text,
                            QuestionId=answerViewModel.QuestionId,
                            
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
                        QuestionnaireId=q.QuestionnaireId,
                        
                        
                        Answers = q.Answers.Select(a => new Answer
                        {
                            Id = a.Id,
                            Text = a.Text,
                            Question=a.Question,
                            QuestionId=a.QuestionId

                            
                            
                            
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

                // Update or add new questions
                foreach (var questionViewModel in viewModel.Questions)
                {
                    var existingQuestion = existingQuestionnaire.Questions.FirstOrDefault(q => q.Id == questionViewModel.Id);


                    if (existingQuestion != null)
                    {
                        existingQuestion.Text = questionViewModel.Text;
                        existingQuestion.Type = questionViewModel.Type;

                      
                       
                            foreach (var answerViewModel in questionViewModel.Answers)
                            {
                                // Check if the answer already exists
                                var existingAnswer = existingQuestion.Answers.FirstOrDefault(a => a.Id == answerViewModel.Id);
                                
                                if (answerViewModel.Id==0)
                                {

                                    existingQuestion.Answers.Add(new Answer { Text = answerViewModel.Text });

                               
                                }
                                else if (string.IsNullOrEmpty(answerViewModel.Text))
                                {

                                 existingQuestion.Answers.Remove(existingAnswer);
                                _context.SaveChanges();

                                 }
                                else if(existingAnswer !=null)
                                {

                                     existingAnswer.Text = answerViewModel.Text;
                                }

                             }

                            


                       
                    }
                    else
                    {
                       
                        var newQuestion = new Question
                        {
                            Text = questionViewModel.Text,
                            Type = questionViewModel.Type,
                            Answers = questionViewModel.Answers?.Select(a => new Answer { Text = a.Text }).ToList() ?? new List<Answer>()
                        };

                        existingQuestionnaire.Questions.Add(newQuestion);
                    }

                    //if (existingQuestion != null)
                    //{
                    //    existingQuestion.Text = questionViewModel.Text;
                    //    existingQuestion.Type = questionViewModel.Type;

                    //    //var answerId = existingQuestion.Answers.Select(x => x.Id).ToList();
                    //    // Update or add new answers
                    //    //foreach (var answerViewModel in questionViewModel.Answers)
                    //    //{
                    //    //    var existingAnswer = existingQuestion.Answers.FirstOrDefault(a => a.Id == answerViewModel.Id);

                    //    //    if (existingAnswer != null)
                    //    //    {
                    //    //        existingAnswer.Text = answerViewModel.Text;
                    //    //    }
                    //    //    else
                    //    //    {
                    //    //        // Handle adding new answers if necessary
                    //    //        existingQuestion.Answers.Add(new Answer { Text = answerViewModel.Text });
                    //    //    }
                    //    //}
                    //    if (questionViewModel.Answers != null)
                    //    {
                    //        // Update or add new answers
                    //        foreach (var answerViewModel in questionViewModel.Answers)
                    //        {
                    //            var existingAnswer = existingQuestion.Answers.FirstOrDefault(a => a.Id == answerViewModel.Id);

                    //            if (existingAnswer != null)
                    //            {
                    //                // Update existing answer
                    //                existingAnswer.Text = answerViewModel.Text;
                    //            }
                    //            else
                    //            {
                    //                foreach (var newanswers in questionViewModel.Answers)
                    //                {
                    //                    existingQuestion.Answers.Add(new Answer { Text = newanswers.Text });
                    //                }
                    //                // Check if the answer with the same text already exists
                    //                //var answerWithSameText = existingQuestion.Answers.FirstOrDefault(a => a.Text == answerViewModel.Text);

                    //                //if (answerWithSameText == null)
                    //                //{
                    //                //    // Add new answer only if it doesn't exist with the same text

                    //                //}
                    //                //else
                    //                //{
                    //                //    // Optionally handle the case where an answer with the same text already exists
                    //                //    // You can choose to do nothing, show a message, or take any other action
                    //                //}
                    //            }
                    //        }
                    //    }

                    //}
                    //else
                    //{
                    //    // Add new question with its answers
                    //    var newQuestion = new Question
                    //    {
                    //        Text = questionViewModel.Text,
                    //        Type = questionViewModel.Type,
                    //        Answers = questionViewModel.Answers.Select(a => new Answer { Text = a.Text }).ToList()
                    //    };

                    //    existingQuestionnaire.Questions.Add(newQuestion);
                    //}
                }

                // Remove any questions that are not in the view model
                //var questionIdsInViewModel = viewModel.Questions.Select(q => q.Id);
                //var questionsToRemove = existingQuestionnaire.Questions.Where(q => !questionIdsInViewModel.Contains(q.Id)).ToList();
                //foreach (var questionToRemove in questionsToRemove)
                //{
                //    existingQuestionnaire.Questions.Remove(questionToRemove);
                //}

                // Save changes to the database
                _questionnaire.Update(existingQuestionnaire);
                await _questionnaire.commitAsync();
                TempData["Success"] = "Questionnaire updated successfully";
                return RedirectToAction("Index");
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
        public IActionResult DeleteConfirm(int id)
        {
            _questionnaire.Delete(id);
            _questionnaire.commitAsync();
            
            return Json(new { success = true, message = "Item deleted successfully" });
            
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
