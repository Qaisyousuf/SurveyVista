using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public enum QuestionnaireStatus
    {
        Draft = 0,      // Can be fully edited/deleted
        Published = 1,  // Live, accepting responses - limited editing  
        Archived = 2    // Completed, read-only
    }
}
