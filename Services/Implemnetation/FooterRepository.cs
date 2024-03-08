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
            //_context.Entry(footer).State = EntityState.Modified;


            //foreach (var fsm in footer.FooterSocialMedias)
            //{
            //    var existingEntity = _context.ChangeTracker.Entries<FooterSocialMedia>()
            //                                   .FirstOrDefault(e => e.Entity.FooterId == fsm.FooterId && e.Entity.SocialId == fsm.SocialId);

            //    if (existingEntity != null)
            //    {
            //        _context.Entry(existingEntity.Entity).State = EntityState.Detached;
            //    }

            //    _context.Entry(fsm).State = EntityState.Modified;
            //}
        }

        public List<Footer> GetFooterWithFooterSocial()
        {
            return _context.Footers.AsNoTracking().Include(x => x.FooterSocialMedias).ThenInclude(x => x.SocialMedia).ToList();
        }

        public Footer GetFooterByIdWithSocialMedia(int id)
        {
            return _context.Footers.AsNoTracking().Include(x => x.FooterSocialMedias).ThenInclude(x => x.SocialMedia).SingleOrDefault(x=>x.Id==id);
        }

      
    }
}
