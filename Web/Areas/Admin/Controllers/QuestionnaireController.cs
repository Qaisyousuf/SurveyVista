using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Model;
using Services.Implemnetation;
using Services.Interaces;
using Web.ViewModel.QuestionnaireVM;
using Web.ViewModel.QuestionVM;

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
        public IActionResult Edit(int id)
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
        public async Task<IActionResult> Edit(QuestionnaireViewModel viewModel)
        {

            var questionTypes = Enum.GetValues(typeof(QuestionType)).Cast<QuestionType>();

            ViewBag.QuestionTypes = new SelectList(questionTypes);
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

                var Question = viewModel.Questions.ToList();

                if(Question.Count()!=0)
                {
                    foreach (var questionViewModel in viewModel.Questions)
                    {

                        var existingQuestion = existingQuestionnaire.Questions.FirstOrDefault(q => q.Id == questionViewModel.Id);

                        if (existingQuestion != null)
                        {
                            existingQuestion.Text = questionViewModel.Text;
                            existingQuestion.Type = questionViewModel.Type;

                            // Update answers
                            foreach (var answerViewModel in questionViewModel.Answers)
                            {
                                var existingAnswer = existingQuestion.Answers.FirstOrDefault(a => a.Id == answerViewModel.Id);

                                if (existingAnswer != null)
                                {
                                    existingAnswer.Text = answerViewModel.Text;
                                    existingAnswer.QuestionId = answerViewModel.QuestionId;
                                }
                                else
                                {
                                    // Handle adding new answers if necessary
                                    existingQuestion.Answers.Add(new Answer { Text = answerViewModel.Text });
                                }
                            }
                        }

                    }
                }
                else
                {
                    foreach (var questionViewModel in viewModel.Questions)
                    {

                        var existingQuestion = existingQuestionnaire.Questions.FirstOrDefault(q => q.Id == questionViewModel.Id);

                        if (existingQuestion != null)
                        {
                            existingQuestion.Text = questionViewModel.Text;
                            existingQuestion.Type = questionViewModel.Type;

                            // Update answers
                            foreach (var answerViewModel in questionViewModel.Answers)
                            {
                                var existingAnswer = existingQuestion.Answers.FirstOrDefault(a => a.Id == answerViewModel.Id);

                                if (existingAnswer != null)
                                {
                                    existingAnswer.Text = answerViewModel.Text;
                                    existingAnswer.QuestionId = answerViewModel.QuestionId;
                                }
                                else
                                {
                                    // Handle adding new answers if necessary
                                    existingQuestion.Answers.Add(new Answer { Text = answerViewModel.Text });
                                }
                            }
                        }

                    }
                }

                // Update questions

               
                

                // Save changes to the database
                _questionnaire.Update(existingQuestionnaire);
                TempData["Success"] = "Questionnaire updated successfully";
                await _questionnaire.commitAsync();

                return RedirectToAction("Index");
            }

            // If the model state is not valid, return to the edit view with the existing model
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
            TempData["Success"] = "Questionnaire deleted successfully";
        }

    }
}
