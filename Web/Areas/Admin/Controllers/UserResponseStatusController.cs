using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Interaces;
using Web.ViewModel.QuestionnaireVM;

namespace Web.Areas.Admin.Controllers
{
    public class UserResponseStatusController : Controller
    {
        private readonly SurveyContext _context;
        private readonly IUserResponseRepository _userResponse;

        public UserResponseStatusController(SurveyContext context,IUserResponseRepository userResponse)
        {
            _context = context;
            _userResponse = userResponse;
        }
        public async Task<IActionResult> Index()
        {
            var usersWithQuestionnaires = await _context.Responses
            .Include(r => r.Questionnaire)
            .GroupBy(r => r.UserEmail)
            .Select(g => new UserResponsesViewModel
            {
                UserName = g.FirstOrDefault().UserName, // Display the first username found for the email
                UserEmail = g.Key,
                Responses = g.Select(r => new Response
                {
                    Questionnaire = r.Questionnaire
                }).Distinct().ToList()
            })
            .ToListAsync();

            return View(usersWithQuestionnaires);
        }


        public async Task<IActionResult> UserResponsesStatus(string userEmail)
        {
            var responses = await _context.Responses
                .Include(r => r.Questionnaire)
                .Include(r => r.ResponseDetails)
                    .ThenInclude(rd => rd.Question)
                        .ThenInclude(q => q.Answers) // Include the Answers entity
                .Include(r => r.ResponseDetails)
                    .ThenInclude(rd => rd.ResponseAnswers)
                .Where(r => r.UserEmail == userEmail)
                .ToListAsync();

            if (responses == null || !responses.Any())
            {
                return NotFound();
            }

            var userName = responses.First().UserName;

            var viewModel = new UserResponsesViewModel
            {
                UserName = userName,
                UserEmail = userEmail,
                Responses = responses
            };

            return View(viewModel);
        }


        //public async Task<IActionResult> UserResponsesStatus(string userEmail)
        //{
        //    var responses = await _context.Responses
        //        .Include(r => r.Questionnaire)
        //        .Include(r => r.ResponseDetails)
        //            .ThenInclude(rd => rd.Question)
        //        .Include(r => r.ResponseDetails)

        //            .ThenInclude(rd => rd.ResponseAnswers)
        //        .Where(r => r.UserEmail == userEmail)
        //        .ToListAsync();

        //    if (responses == null || !responses.Any())
        //    {
        //        return NotFound();
        //    }

        //    var userName = responses.First().UserName;

        //    var viewModel = new UserResponsesViewModel
        //    {
        //        UserName = userName,
        //        UserEmail = userEmail,
        //        Responses = responses
        //    };

        //    return View(viewModel);
        //}

    }
}
