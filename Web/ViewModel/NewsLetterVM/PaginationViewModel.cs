using Model;

namespace Web.ViewModel.NewsLetterVM
{
    public class PaginationViewModel
    {
        public IEnumerable<NewsLetterViewModel>? Subscriptions { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }
}
