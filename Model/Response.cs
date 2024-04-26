using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Response
    {
        public int Id { get; set; }
        public int QuestionnaireId { get; set; } // Foreign Key to Questionnaire

        [ForeignKey("QuestionnaireId")]
        public Questionnaire? Questionnaire { get; set; }
        public string? UserName { get; set; } // To store the user's name
        public string? UserEmail { get; set; } // To store the user's email
        public DateTime SubmissionDate { get; set; }
        public List<ResponseDetail> ResponseDetails { get; set; } = new List<ResponseDetail>();
    }
}
