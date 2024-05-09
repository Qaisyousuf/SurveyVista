using Mailjet.Client.Resources;
using Mailjet.Client;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Services.EmailSend;
using Services.Interaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Implemnetation
{
    public class EmailStatsService: IEmailStatsService
    {
        private readonly IConfiguration _configuration;

        public EmailStatsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<EmailStatistic> FetchEmailStatsAsync()
        {

            var apiKey = _configuration["MailJet:ApiKey"];
            var apiSecret = _configuration["MailJet:SecretKey"];

            MailjetClient client = new MailjetClient(apiKey, apiSecret);
            

            // Construct the request to get statistics
            MailjetRequest request = new MailjetRequest
            {
                Resource = Statcounters.Resource,
            }
            .Property(Statcounters.CounterSource, "APIKey")  // assuming you want statistics based on API Key usage
            .Property(Statcounters.CounterTiming, "Message")  // assuming you want statistics about messages
            .Property(Statcounters.CounterResolution, "Lifetime");  // assuming you want lifetime statistics

            // Execute the request
            MailjetResponse response = await client.GetAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return ParseEmailStats(response.GetData());
            }
            else
            {
                throw new InvalidOperationException($"Failed to fetch data: {response.StatusCode}, {response.GetErrorMessage()}");
            }
        }

        private EmailStatistic ParseEmailStats(JToken data)
        {
            EmailStatistic stats = new EmailStatistic
            {
                TotalEmails = data[0]["Total"].Value<int>(),
                Delivered = data[0]["Delivered"].Value<int>(),
                Opened = data[0]["Opened"].Value<int>(),
                Clicked = data[0]["Clicked"].Value<int>(),
                Bounced = data[0]["Bounced"].Value<int>(),
                Blocked = data[0]["Blocked"].Value<int>(),
                MarkedAsSpam = data[0]["Spam"].Value<int>()
            };
            return stats;
        }
    }
}
