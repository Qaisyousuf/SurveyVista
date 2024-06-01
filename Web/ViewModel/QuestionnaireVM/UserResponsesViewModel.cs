

using Model;

namespace Web.ViewModel.QuestionnaireVM
{
    public class UserResponsesViewModel
    {
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public List<Response>? Responses { get; set; }
    }
}
