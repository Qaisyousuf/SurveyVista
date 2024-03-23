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
    public class QuestionnaireRepository : IQuestionnaireRepository
    {
        private readonly SurveyContext _context;

        public QuestionnaireRepository(SurveyContext Context)
        {
            _context = Context;
        }
        public void Add(Questionnaire questionnaire)
        {
            _context.Questionnaires.Add(questionnaire);
        }

        public async Task commitAsync()
        {
            await _context.SaveChangesAsync();
        }

        //public void Delete(int? id)
        //{
        //    var questionnairId = GetQuesById(id);

        //    _context.Questionnaires.Remove(questionnairId);
        //}

        public List<Questionnaire> GetAllQuestions()
        {
            return _context.Questionnaires.ToList();
        }

        public Questionnaire GetQuesById(int? id)
        {
            return _context.Questionnaires.Find(id);
        }

        public List<Questionnaire> GetQuestionnairesWithQuestion()
        {
           return _context.Questionnaires.AsNoTracking().Include(x=>x.Questions).ThenInclude(x=>x.Answers).ToList();
        }

        public Questionnaire GetQuestionnaireWithQuestionAndAnswer(int? id)
        {
            return _context.Questionnaires.AsNoTracking().Include(x => x.Questions).ThenInclude(x => x.Answers).FirstOrDefault(x=>x.Id==id);
        }

        public async Task Update(Questionnaire questionnaire)
        {

            _context.Questionnaires.Update(questionnaire);

             await _context.SaveChangesAsync();

        }

        public async Task Delete(int? id)
        {
            var questionnairId = GetQuesById(id);

          _context.Questionnaires.Remove(questionnairId);
             await _context.SaveChangesAsync();
        }
    }
}
