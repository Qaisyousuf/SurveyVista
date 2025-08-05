using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.EmailSend
{
    public class EmailToSend
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string HtmlBody { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public EmailToSend(string to, string subject, string htmlBody)
        {
            To = to;
            Subject = subject;
            HtmlBody = htmlBody;
            Headers = new Dictionary<string, string>(); // optional unless needed
        }
    }


}
