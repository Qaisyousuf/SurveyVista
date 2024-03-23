using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface IQuestionnaireRepository
    {
        List<Questionnaire> GetAllQuestions();
        List<Questionnaire> GetQuestionnairesWithQuestion();
        Questionnaire GetQuesById(int? id);
        Questionnaire GetQuestionnaireWithQuestionAndAnswer(int? id);

        void Add(Questionnaire questionnaire);
        Task Update(Questionnaire questionnaire);
        Task Delete(int? id);

        Task commitAsync();
    }
}
