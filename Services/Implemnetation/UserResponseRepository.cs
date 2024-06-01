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
    public class UserResponseRepository : IUserResponseRepository
    {
        private readonly SurveyContext _context;

        public UserResponseRepository(SurveyContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<Response>> GetResponsesByUserAsync(string userName)
        {
            return await _context.Responses
            .Include(r => r.Questionnaire)
            .Include(r => r.ResponseDetails)
                .ThenInclude(rd => rd.Question)
            .Include(r => r.ResponseDetails)
                .ThenInclude(rd => rd.ResponseAnswers)
            .Where(r => r.UserName == userName)
            .ToListAsync();
        }
    }
}
