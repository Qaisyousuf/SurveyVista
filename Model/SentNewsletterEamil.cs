using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class SentNewsletterEamil
    {
        public int Id { get; set; }                 
        public string? RecipientEmail { get; set; } 
        public string? Subject { get; set; }        
        public string? Body { get; set; }           
        public DateTime SentDate { get; set; }      
        public DateTime ReceivedActivity { get; set; }
        public bool IsDelivered { get; set; }       
        public bool IsOpened { get; set; }          
        public bool IsClicked { get; set; }         
        public bool IsBounced { get; set; }         
        public bool IsSpam { get; set; }
        public bool IsSent { get; set; }
        public bool IsUnsubscribed { get; set; }
        public bool IsBlocked { get; set; }

    }
}
