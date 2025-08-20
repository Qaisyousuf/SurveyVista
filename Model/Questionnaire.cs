using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
   
    public class Questionnaire
    {
        public Questionnaire()
        {
            Questions = new List<Question>();

            // ADD THESE NEW LINES (with safe defaults):
            Status = QuestionnaireStatus.Draft; // Default to Draft
            CreatedDate = DateTime.UtcNow;      // Default to now
        }

        // EXISTING PROPERTIES (keep as-is):
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<Question>? Questions { get; set; }

        // ADD THESE NEW PROPERTIES:
        public QuestionnaireStatus Status { get; set; } = QuestionnaireStatus.Draft;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedDate { get; set; }    // Nullable - only set when published
        public DateTime? ArchivedDate { get; set; }     // Nullable - only set when archived
    }
}
