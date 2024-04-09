using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class SentQuestionnaire
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }

        public int QuestionnaireId { get; set; }
        [ForeignKey("QuestionnaireId")]
        public Questionnaire? Questionnaire { get; set; }
    }
}
