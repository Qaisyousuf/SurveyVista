using Microsoft.EntityFrameworkCore.Migrations;
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

        // ADD THESE NEW METHOD SIGNATURES:
        List<Questionnaire> GetQuestionnairesByStatus(QuestionnaireStatus status);
        List<Questionnaire> GetAllQuestionnairesWithStatus();
        Task<bool> HasResponses(int questionnaireId);
        Task UpdateStatus(int questionnaireId, QuestionnaireStatus newStatus);
    }

  
}
