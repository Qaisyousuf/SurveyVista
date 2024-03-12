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
    public class QuestionRepository : IQuestionRepository
    {
        private readonly SurveyContext _context;

        public QuestionRepository(SurveyContext context)
        {
            _context = context;
        }
        public void Add(Question question)
        {
           _context.Questions.Add(question);
        }

        public async Task commitAsync()
        {
            await _context.SaveChangesAsync();
        }

        public List<Question> GetAllQuestions()
        {
            throw new NotImplementedException();
        }

        public Question GetQuestionById(int id)
        {
            throw new NotImplementedException();
        }

        public List<Question> GetQuestionsWithAnswers()
        {
           return _context.Questions.AsNoTracking().Include(x=>x.Answers).ToList();
        }
    }
}
