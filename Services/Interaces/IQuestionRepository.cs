using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface IQuestionRepository
    {
        List<Question> GetAllQuestions();

        List<Question> GetQuestionsWithAnswers();

        Question GetQuestionById(int id);

        void Add(Question question);

        Task commitAsync();
    }
}
