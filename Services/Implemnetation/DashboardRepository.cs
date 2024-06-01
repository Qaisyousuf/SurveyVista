using Data;
using Microsoft.EntityFrameworkCore;
using Services.Interaces;

namespace Services.Implemnetation
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly SurveyContext _context;

        public DashboardRepository(SurveyContext Context)
        {
            _context = Context;
        }
        public async Task<Dictionary<string, int>> GetModelCountsAsync()
        {
            var counts = new Dictionary<string, int>
        {
            { "Pages", await _context.Pages.CountAsync() },
            { "Banners", await _context.Banners.CountAsync() },
            { "Addresses", await _context.Addresss.CountAsync() },
            { "Footers", await _context.Footers.CountAsync() },
            { "SocialMedia", await _context.SocialMedia.CountAsync() },
            { "FooterSocialMedias", await _context.FooterSocialMedias.CountAsync() },
            { "Subscriptions", await _context.Subscriptions.CountAsync() },
            { "SentNewsletterEmails", await _context.SentNewsletterEamils.CountAsync() }
        };

            return counts;
        }

        public async Task<Dictionary<string, int>> GetCurrentBannerSelectionsAsync()
        {
            return await _context.Pages
                .GroupBy(p => p.banner.Title)
                .Select(g => new { BannerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.BannerId.ToString(), g => g.Count);
        }

        public async Task<Dictionary<string, int>> GetCurrentFooterSelectionsAsync()
        {
            return await _context.Pages
                .GroupBy(p => p.footer.Title)
                .Select(g => new { FooterId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.FooterId.ToString(), g => g.Count);
        }
    }
}
