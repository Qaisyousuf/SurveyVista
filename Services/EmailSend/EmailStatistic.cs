using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.EmailSend
{
    public class EmailStatistic
    {
        public int TotalEmails { get; set; }
        public int Delivered { get; set; }
        public int Opened { get; set; }
        public int Clicked { get; set; }
        public int Bounced { get; set; }
        public int Blocked { get; set; }
        public int MarkedAsSpam { get; set; }
    }
}
