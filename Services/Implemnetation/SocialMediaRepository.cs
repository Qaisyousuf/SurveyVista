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
    public class SocialMediaRepository:ISocialMediaRepository
    {
        private readonly SurveyContext _context;

        public SocialMediaRepository(SurveyContext Context)
        {
            _context = Context;
        }

        public async Task Add(SocialMedia socialMedia)
        {
           await _context.SocialMedia.AddAsync(socialMedia);
        }

        public async Task commitAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Delete(int id)
        {
            var socialmedia=GetSocialById(id);

            _context.SocialMedia.Remove(socialmedia);
        }

        public List<SocialMedia> GetListofSocialMediaById(List<int> ids)
        {

            return ids.Select(id => GetSocialById(id)).ToList();

        }

        public SocialMedia GetSocialById(int id)
        {
            return _context.SocialMedia.AsNoTracking().Where(x => x.Id == id).FirstOrDefault();
        }

        public List<SocialMedia> GetSocialMedia()
        {
            return _context.SocialMedia.AsNoTracking().ToList();
        }

        public void Update(SocialMedia socialMedia)
        {
            _context.SocialMedia.Update(socialMedia);
        }
    }
}
