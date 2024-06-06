namespace Web.ViewModel.DashboardVM
{
    public class DashboardViewModel
    {
        public Dictionary<string, int>? ModelCounts { get; set; }
        public Dictionary<string, int>? BannerSelections { get; set; }
        public Dictionary<string, int>? FooterSelections { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public List<PerformanceDataViewModel>? PerformanceData { get; set; }
        public List<VisitorDataViewModel>? VisitorData { get; set; } // New property


    }
}
