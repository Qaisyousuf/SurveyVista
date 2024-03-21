using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Answer
    {
        public int Id { get; set; }
        
        public string? Text { get; set; }
        public int QuestionId { get; set; } // Foreign key for Question
        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }
    }
}
