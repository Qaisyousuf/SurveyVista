using Data;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Interaces;

namespace Services.Implemnetation
{
    public class PageRepository : IPageRepository
    {
        private readonly SurveyContext _context;

        public PageRepository(SurveyContext context)
        {
            _context = context;
        }
        public void Add(Page page)
        {
            _context.Pages.Add(page);
        }

        public async Task commitAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Delete(int id)
        {
            var page = GetPageById(id);

            _context.Pages.Remove(page);
        }

        public Page GetPageById(int id)
        {
            return _context.Pages.AsNoTracking().Where(x => x.Id == id).FirstOrDefault();
        }

        public async Task<List<Page>> GetPages()
        {
            return await _context.Pages.ToListAsync();
        }

        public Page GetPageSlug(string slug)
        {
            return _context.Pages.Where(x => x.Slug == slug).AsNoTracking().FirstOrDefault();
        }

        public List<Page> GetPageWithAll()
        {

            return _context.Pages.Include(x => x.banner).Include(x => x.footer).AsNoTracking().ToList();
        }

        public List<Page> GetPageWithBanner()
        {
            return _context.Pages.Include(x => x.banner).AsNoTracking().ToList();
        }

        public List<Page> GetPageWithFooter()
        {

            return _context.Pages.Include(x => x.footer).AsNoTracking().ToList();
        }

        public bool SlugExists(string slug, int? pageIdExclude = null)
        {
            if (pageIdExclude != null)
            {
                return _context.Pages.Where(x => x.Id != pageIdExclude).Any(x => x.Slug == slug);
            }

            return _context.Pages.Any(x => x.Slug == slug);
        }

        public void Update(Page page)
        {
            _context.Pages.Update(page);
        }
    }
}
