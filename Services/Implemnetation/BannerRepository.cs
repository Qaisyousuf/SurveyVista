using Data;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Interaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Implemnetation
{
    public class BannerRepository : IBannerRepository
    {
        private readonly SurveyContext _context;

        public BannerRepository(SurveyContext context)
        {
            _context = context;
        }

        public async Task Add(Banner banner)
        {
            await _context.Banners.AddAsync(banner);
        }

        public async Task commitAsync()
        {
             await _context.SaveChangesAsync();
        }

        public  void Delete(int id)
        {
            var banner = GetBannerById(id);

            _context.Banners.Remove(banner);

        }

        public List<Banner> GetAllBanners()
        {
            return _context.Banners.AsNoTracking().ToList();
        }

        public Banner GetBannerById(int id)
        {
            return _context.Banners.AsNoTracking().Where(x => x.Id == id).FirstOrDefault();
        }

        public async Task<IEnumerable<Banner>> GetBanners()
        {
           return await _context.Banners.AsNoTracking().ToListAsync();
        }

        public void Update(Banner banner)
        {
             _context.Banners.Update(banner);
        }
    }
}
