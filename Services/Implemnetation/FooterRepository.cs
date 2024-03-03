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
    public class FooterRepository:IFooterRepository
    {
        private readonly SurveyContext _context;

        public FooterRepository(SurveyContext Context)
        {
            _context = Context;
        }

        public async Task Add(Footer footer)
        {
            await _context.Footers.AddAsync(footer);
        }

        public async Task commitAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Delete(int id)
        {
            var footerWithId=GetFooterById(id);

            _context.Footers.Remove(footerWithId);
        }

        public List<Footer> GetFooter()
        {
            return _context.Footers.AsNoTracking().Include(x=>x.FooterSocialMedias).ThenInclude(x=>x.SocialMedia).ToList();
        }

       
        public Footer GetFooterById(int id)
        {
            return _context.Footers.AsNoTracking().Include(y => y.FooterSocialMedias).Where(x => x.Id == id).FirstOrDefault();
        }

        public void Update(Footer footer)
        {
            _context.Footers.Update(footer);
        }

        public List<Footer> GetFooterWithFooterSocial()
        {
            return _context.Footers.AsNoTracking().Include(x => x.FooterSocialMedias).ThenInclude(x => x.SocialMedia).ToList();
        }
    }
}
