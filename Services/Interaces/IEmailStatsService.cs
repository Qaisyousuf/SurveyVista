using Services.EmailSend;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interaces
{
    public interface IEmailStatsService
    {
        Task<EmailStatistic> FetchEmailStatsAsync();
    }
}
