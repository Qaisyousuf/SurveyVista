using Microsoft.AspNetCore.Mvc;
using Services.Interaces;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Web.ViewModel.QuestionnaireVM;

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

        public IActionResult Error()
        {
            ViewBag.ErrorMessage = "The survey link has expired. request a new link.";

            return View();
        }

        public IActionResult DisplayQuestionnaire(int id, string token)
        {
            // Check if the token is null or empty
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.ErrorMessage = "The URL is invalid. Please provide a valid token.";
                return View("Error");
            }

            // Split the token to extract the expiration date and time
            string[] tokenParts = token.Split('|');
            if (tokenParts.Length != 2)
            {
                ViewBag.ErrorMessage = "The URL is invalid. Please provide a valid token.";
                return View("Error");
            }

            string expiryDateTimeString = tokenParts[1];

            // Parse the expiration datetime in UTC format
            if (!DateTime.TryParseExact(expiryDateTimeString, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime expiryDateTimeUtc))
            {
                ViewBag.ErrorMessage = "The URL is invalid. Please provide a valid token.";
                return View("Error");
            }

            // Convert the expiration datetime to local time
            DateTime expiryDateTimeLocal = expiryDateTimeUtc.ToLocalTime();

            // Check if the token is expired (accounting for UTC+2 offset)
            if (expiryDateTimeLocal < DateTime.Now.AddHours(2))
            {

                return RedirectToAction(nameof(Error));
            }

            // Retrieve the questionnaire using the numeric ID
            var questionnaire = _questionnaireRepository.GetQuestionnaireWithQuestionAndAnswer(id);

            return View(questionnaire);
        }


        
    }
}
