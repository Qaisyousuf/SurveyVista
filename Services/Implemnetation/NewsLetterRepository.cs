using Data;
using Microsoft.EntityFrameworkCore;
using Model;
using Services.Interaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Services.Implemnetation
{
    public class NewsLetterRepository : INewsLetterRepository
    {
        private readonly SurveyContext _context;

        public NewsLetterRepository(SurveyContext context)
        {
            _context = context;
        }

        public void Delete(int id)
        {
            var IdToDelete = GetById(id);

            _context.Subscriptions.Remove(IdToDelete);
        }

      
        public  List<Subscription> GetAll()
        {
            return  _context.Subscriptions.AsNoTracking().ToList();
        }

        public Subscription GetById(int id)
        {
            return _context.Subscriptions.AsNoTracking().Where(x => x.Id == id).FirstOrDefault();
        }

        
    }
}
