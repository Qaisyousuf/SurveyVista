using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Model;
using Services.Interaces;
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

            List<QuestionnaireViewModel> viewmodel = new List<QuestionnaireViewModel>();


            foreach (var item in questionnaire)
            {
                viewmodel.Add(new QuestionnaireViewModel
                {
                    Id = item.Id,
                    Description = item.Description,
                    Title = item.Title,
                    Questions = item.Questions
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
                        Answers = new List<Answer>() // Initialize the list of answers for each question
                    };

                    foreach (var answerViewModel in questionViewModel.Answers)
                    {
                        var answer = new Answer
                        {
                            Text = answerViewModel.Text,
                            QuestionId=answerViewModel.QuestionId,
                            
                        };

                        // Add the answer to the list of answers for the current question
                        question.Answers.Add(answer);
                    }

                    // Add the question to the list of questions for the questionnaire
                    questionnaire.Questions.Add(question);
                }


                //var answers = questions.Where(x => x.Answers == viewmodel.Answers);


                //foreach (var question in questions)
                //{

                //    questionnaire.Questions.Add(new Question
                //    {
                //        Id = question.Id,
                //        Text=question.Text,
                //        Type=question.Type,
                //        QuestionnaireId=questionnaire.Id,


                //    });

                //    //foreach(var answer in answers)
                //    //{
                //    //    question.Answers.Add(new Answer
                //    //    {
                //    //        Id=answer
                //    //    });
                //    //}


                //}

                _questionnaire.Add(questionnaire);
              await _questionnaire.commitAsync();
                TempData["Success"] = "Questionnaire created successfully";



                return RedirectToAction("Index"); 
            }
            return View(viewmodel);
        }
        
    }
}
