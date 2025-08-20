using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Interaces;
using Web.ViewModel.QuestionnaireVM;

namespace Web.Areas.Admin.Controllers
{


    public class UserResponseController : Controller
    {
        private readonly SurveyContext _context;
        private readonly IUserResponseRepository _userResponse;
        private readonly ILogger<UserResponseController> _logger;

        public UserResponseController(
            SurveyContext context,
            IUserResponseRepository userResponse,
            ILogger<UserResponseController> logger)
        {
            _context = context;
            _userResponse = userResponse;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var responses = await GetAllResponsesWithDetailsAsync();
                return View(responses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving responses");
                TempData["Error"] = "Error loading responses. Please try again.";
                return View(new List<Response>());
            }
        }

        private async Task<List<Response>> GetAllResponsesWithDetailsAsync()
        {
            return await _context.Responses
                .Include(r => r.Questionnaire)
                .OrderByDescending(r => r.SubmissionDate) // Most recent first
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> ViewResponse(int id)
        {
            try
            {
                var response = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.Question)
                            .ThenInclude(q => q.Answers)
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .Include(r => r.Questionnaire)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (response == null)
                {
                    TempData["Error"] = "Response not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving response {ResponseId}", id);
                TempData["Error"] = "Error loading response details.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> UserResponsesStatus(string userName)
        {
            try
            {
                var responses = await _userResponse.GetResponsesByUserAsync(userName);
                if (responses == null || !responses.Any())
                {
                    TempData["Warning"] = "No responses found for this user.";
                    return RedirectToAction(nameof(Index));
                }

                var userEmail = responses.First().UserEmail;
                var viewModel = new UserResponsesViewModel
                {
                    UserName = userName,
                    UserEmail = userEmail,
                    Responses = responses.ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user responses for {UserName}", userName);
                TempData["Error"] = "Error loading user responses.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var response = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (response == null)
                {
                    TempData["Error"] = "Response not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Remove related data first
                if (response.ResponseDetails != null)
                {
                    foreach (var detail in response.ResponseDetails)
                    {
                        if (detail.ResponseAnswers != null)
                        {
                            _context.ResponseAnswers.RemoveRange(detail.ResponseAnswers);
                        }
                    }
                    _context.ResponseDetails.RemoveRange(response.ResponseDetails);
                }

                _context.Responses.Remove(response);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Response {ResponseId} deleted successfully", id);
                TempData["Success"] = "Response deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting response {ResponseId}", id);
                TempData["Error"] = "Error deleting response. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                TempData["Warning"] = "No responses selected for deletion.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _logger.LogInformation("Attempting to delete {Count} responses: {Ids}", ids.Count, string.Join(", ", ids));

                var responses = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .Where(r => ids.Contains(r.Id))
                    .ToListAsync();

                if (!responses.Any())
                {
                    TempData["Warning"] = "No responses found to delete.";
                    return RedirectToAction(nameof(Index));
                }

                // Remove related data first
                foreach (var response in responses)
                {
                    if (response.ResponseDetails != null)
                    {
                        foreach (var detail in response.ResponseDetails)
                        {
                            if (detail.ResponseAnswers != null)
                            {
                                _context.ResponseAnswers.RemoveRange(detail.ResponseAnswers);
                            }
                        }
                        _context.ResponseDetails.RemoveRange(response.ResponseDetails);
                    }
                }

                _context.Responses.RemoveRange(responses);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted {Count} responses", responses.Count);
                TempData["Success"] = $"Successfully deleted {responses.Count} response{(responses.Count > 1 ? "s" : "")}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting multiple responses. IDs: {Ids}", string.Join(", ", ids));
                TempData["Error"] = "Error deleting responses. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                var allResponses = await _context.Responses
                    .Include(r => r.ResponseDetails)
                        .ThenInclude(rd => rd.ResponseAnswers)
                    .ToListAsync();

                if (!allResponses.Any())
                {
                    TempData["Warning"] = "No responses to delete.";
                    return RedirectToAction(nameof(Index));
                }

                // Remove all related data
                foreach (var response in allResponses)
                {
                    if (response.ResponseDetails != null)
                    {
                        foreach (var detail in response.ResponseDetails)
                        {
                            if (detail.ResponseAnswers != null)
                            {
                                _context.ResponseAnswers.RemoveRange(detail.ResponseAnswers);
                            }
                        }
                        _context.ResponseDetails.RemoveRange(response.ResponseDetails);
                    }
                }

                _context.Responses.RemoveRange(allResponses);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted all {Count} responses", allResponses.Count);
                TempData["Success"] = $"Successfully deleted all {allResponses.Count} responses.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all responses");
                TempData["Error"] = "Error deleting all responses. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        // API endpoint to check if responses exist
        [HttpGet]
        public async Task<IActionResult> CheckResponseExists(int id)
        {
            var exists = await _context.Responses.AnyAsync(r => r.Id == id);
            return Json(new { exists });
        }

        // API endpoint to get response count
        [HttpGet]
        public async Task<IActionResult> GetResponseCount()
        {
            var count = await _context.Responses.CountAsync();
            return Json(new { count });
        }
    }
}
