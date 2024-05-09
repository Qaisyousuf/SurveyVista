using Model;

namespace Web.ViewModel.NewsLetterVM
{
    public class EmailStatsViewModel
    {
        public IEnumerable<SentNewsletterEamil>? Emails { get; set; }
        public IEnumerable<dynamic>? DailyActivities { get; set; }
    }
}
