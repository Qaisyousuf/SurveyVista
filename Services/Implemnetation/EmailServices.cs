using Mailjet.Client;
using Mailjet.Client.Resources;
using Mailjet.Client.TransactionalEmails;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Services.EmailSend;
using Services.Interaces;


namespace Services.Implemnetation
{
    public class EmailServices : IEmailServices
    {
        private readonly IConfiguration _configuration;

        public EmailServices(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendConfirmationEmailAsync(EmailToSend emailSend)
        {
            var apiKey = _configuration["MailJet:ApiKey"];
            var secretKey = _configuration["MailJet:SecretKey"];
            var fromEmail = _configuration["Email:From"];
            var fromName = _configuration["Email:ApplicationName"];

            var client = new MailjetClient(apiKey, secretKey);

            var message = new JObject
            {
                ["From"] = new JObject
                {
                    ["Email"] = fromEmail,
                    ["Name"] = fromName
                },
                ["To"] = new JArray
            {
                new JObject
                {
                    ["Email"] = emailSend.To,
                    ["Name"] = emailSend.To.Split('@')[0]
                }
            },
                ["Subject"] = emailSend.Subject,
                ["HTMLPart"] = emailSend.HtmlBody
            };

            // ✨ Add headers if any
            if (emailSend.Headers != null && emailSend.Headers.Any())
            {
                message["Headers"] = JObject.FromObject(emailSend.Headers);
            }

            var request = new MailjetRequest
            {
                Resource = SendV31.Resource
            }
            .Property(Send.Messages, new JArray { message });

            var response = await client.PostAsync(request);
            return response.IsSuccessStatusCode;
        }
    }

}
