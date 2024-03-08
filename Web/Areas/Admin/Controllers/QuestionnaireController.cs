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

        public QuestionnaireController(IQuestionnaireRepository Questionnaire)
        {
            _questionnaire = Questionnaire;
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
                Questions = new List<Question>()
            };

            return View(questionnaire);
        }
        [HttpPost]
        public IActionResult Create(QuestionnaireViewModel viewmodel)
        {
            if(ModelState.IsValid)
            {

                var questionnaire = new Questionnaire
                {
                    Id = viewmodel.Id,
                    Title = viewmodel.Title,
                  Description = viewmodel.Description,
                    
                };

                foreach (var item in viewmodel.Questions)
                {
                    questionnaire.Questions.Add(item);
                }

                _questionnaire.Add(questionnaire);

                _questionnaire.commitAsync();


                return RedirectToAction(nameof(Index));
            }

            return View(viewmodel);
        }
    }
}
