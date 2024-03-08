using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Question
    {
        public int Id { get; set; }
        public string? Text { get; set; }
        public QuestionType Type { get; set; }

        // Foreign key for Questionnaire
      
        public int QuestionnaireId { get; set; } // Foreign key for Questionnaire
        [ForeignKey("QuestionnaireId")]
        public Questionnaire? Questionnaire { get; set; }

        public List<Answer>? Answers { get; set; }
    }
}
