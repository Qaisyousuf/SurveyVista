using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Model;

namespace Web.Areas.Admin.Controllers
{
    public class UserResponseController : Controller
    {
        private readonly SurveyContext _context;

        public UserResponseController(SurveyContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var responses = await GetAllResponsesWithDetailsAsync(); // Fetch the data
            return View(responses); // Pass the data to the view
        }

        private async Task<List<Response>> GetAllResponsesWithDetailsAsync()
        {
            return await _context.Responses
                .Include(r => r.Questionnaire) // Ensure the Questionnaire data is included
                .OrderBy(r => r.Id) // Optional: Order by submission date
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> ViewResponse(int id) // Pass the response ID
        {
                        var response = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                            .ThenInclude(q => q.Answers) // Load all possible answers for the questions
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers) // Load the answers selected by the user
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == id);

            if (response == null)
            {
                return NotFound(); // If no response is found, return a NotFound result
            }

            return View(response); // Pass the response to the view
        }
    }
}
