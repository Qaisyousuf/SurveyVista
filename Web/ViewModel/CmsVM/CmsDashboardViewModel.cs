
using Model;
using System.Collections.Generic;

namespace Web.ViewModel.CmsVM
{
    /// <summary>
    /// Main ViewModel for the unified CMS Dashboard.
    /// Holds all entity lists for tabs + counts for overview.
    /// </summary>
    public class CmsDashboardViewModel
    {
        // ── Entity Lists (for tab content) ──
        public List<Page> Pages { get; set; } = new();
        public List<Banner> Banners { get; set; } = new();
        public List<Footer> Footers { get; set; } = new();
        public List<SocialMedia> SocialMedias { get; set; } = new();
        public List<Address> Addresses { get; set; } = new();

        // ── Counts (for overview stats) ──
        public int PageCount => Pages.Count;
        public int BannerCount => Banners.Count;
        public int FooterCount => Footers.Count;
        public int SocialMediaCount => SocialMedias.Count;
        public int AddressCount => Addresses.Count;
        public int TotalItems => PageCount + BannerCount + FooterCount + SocialMediaCount + AddressCount;

        // ── Quick Lookups (for dropdowns in Page create/edit modal) ──
        /// <summary>
        /// Used in Page create/edit modal — dropdown to select a Banner
        /// </summary>
        public List<BannerSelectItem> BannerOptions { get; set; } = new();

        /// <summary>
        /// Used in Page create/edit modal — dropdown to select a Footer
        /// </summary>
        public List<FooterSelectItem> FooterOptions { get; set; } = new();

        /// <summary>
        /// Used in Footer edit modal — checkboxes to assign Social Media links
        /// </summary>
        public List<SocialMediaSelectItem> SocialMediaOptions { get; set; } = new();
    }

    /// <summary>
    /// Lightweight item for Banner dropdown in Page form
    /// </summary>
    public class BannerSelectItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>
    /// Lightweight item for Footer dropdown in Page form
    /// </summary>
    public class FooterSelectItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Lightweight item for Social Media checkboxes in Footer form
    /// </summary>
    public class SocialMediaSelectItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}