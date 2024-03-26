using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Question
    {
        public Question()
        {
            Answers=new List<Answer>();
        }
        public int Id { get; set; }

        [DisplayName("Question")]
      
        public string? Text { get; set; }
        public QuestionType Type { get; set; }


        public int QuestionnaireId { get; set; }
        [ForeignKey("QuestionnaireId")]
        public Questionnaire? Questionnaire { get; set; }

        public List<Answer> Answers { get; set; } 
    }
}
