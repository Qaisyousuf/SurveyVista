using Microsoft.AspNetCore.Mvc;
using Services.Interaces;
using System.Security.Cryptography;
using System.Text;

namespace Web.Controllers
{
    public class QuestionnaireResponseController : Controller
    {
        private readonly IQuestionnaireRepository _questionnaireRepository;

        public QuestionnaireResponseController(IQuestionnaireRepository questionnaireRepository)
        {
            _questionnaireRepository = questionnaireRepository;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult DisplayQuestionnaire(int id)
        {

            // Retrieve the questionnaire using the numeric ID
            var questionnaire = _questionnaireRepository.GetQuestionnaireWithQuestionAndAnswer(id);
           
            
            // Display the questionnaire
            return View(questionnaire);
        }

      
    }
}
