using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Services.EmailSend;


namespace Services.Interaces
{
    public interface IEmailServices
    {
        Task<bool> SendConfirmationEmailAsync(EmailToSend emailSend);
    }
}
