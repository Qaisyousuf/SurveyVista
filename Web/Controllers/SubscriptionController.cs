using Data;
using Mailjet.Client;
using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Mvc;
using Model;
using Services.Implemnetation;
using Newtonsoft.Json.Linq;
using Services.Interaces;
using Services.EmailSend;
using Microsoft.EntityFrameworkCore;


namespace Web.Controllers
{
    public class SubscriptionController : Controller
    {
        private readonly SurveyContext _context;
        private readonly IEmailServices _mailSerivces;
        private readonly IConfiguration _configuration;

        public SubscriptionController(SurveyContext context,IEmailServices mailSerivces,IConfiguration configuration)
        {
            _context = context;
            _mailSerivces = mailSerivces;
            _configuration = configuration;
        }

        // GET: /Subscription/Subscribe
        public IActionResult Subscribe()
        {
            return View();
        }

        // POST: /Subscription/Subscribe
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(Subscription subscription)
        {

            if(ModelState.IsValid)
            {
                try
                {
                    string email = subscription.Email;
                    string senderName = email.Substring(0, email.IndexOf('@')).ToUpper();

                    // Check if the email already exists
                    var existingSubscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Email == email);
                    if (existingSubscription != null)
                    {

                        TempData["error"] = "Email already exists.";
                        return RedirectToAction("", "home");
                    }

                    string subject = "Subscription Confirmation";
                    string confirmationPath = _configuration["Email:ConfirmEmailPath"]; // Retrieve the confirmation path from appsettings

                    string confirmationUrl = $"{Request.Scheme}://{Request.Host}/{confirmationPath}?email={email}"; // Construct the confirmation URL
                                                                                                                    // Construct the confirmation URL
                    string body = $@"Dear {senderName},<br><br>
                    Thank you for subscribing. We're thrilled to have you on board!<br><br>
                    To confirm your subscription, please click the following button:<br><br>
                    <a href=""{confirmationUrl}"" style=""display: inline-block; padding: 10px 20px; background-color: #28a745; color: #fff; text-decoration: none;"">Confirm Subscription</a><br><br>
                    If you have any questions or need assistance, feel free to contact us at help@seosoft.dk<br><br>
                    Best regards,<br>
                    <h5>
                        Søren Eggert Lundsteen Olsen<br>
                        Seosoft ApS
                    </h5>
                      ";

                    var newEmail = new EmailToSend(email, subject, body);

                    await _mailSerivces.SendConfirmationEmailAsync(newEmail);

                    var subscriber = new Subscription
                    {
                        Name = senderName,
                        Email = subscription.Email,
                        IsSubscribed = false
                    };

                    _context.Subscriptions.Add(subscriber);
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Subscription successful.";
                    return RedirectToAction("", "home");
                }
                catch (Exception ex)
                {
                    TempData["error"] = "Failed to subscribe.";
                    return RedirectToAction("", "home");
                }
            }

            return RedirectToAction("", "home");




        }



        //private async Task SendConfirmationEmail(Subscription subscriber)
        //{
        //    var confirmationLink = Url.Action("Unsubscribe", "Subscription", new { id = subscriber.Id }, protocol: HttpContext.Request.Scheme);

        //    var request = new MailjetRequest
        //    {
        //        Resource = Send.Resource,
        //    }
        //    .Property(Send.Messages, new JArray
        //    {
        //    new JObject
        //    {
        //        {
        //            "From",
        //            new JObject
        //            {
        //                {"Email", "qais@seosoft.dk"},
        //                {"Name", "SeoSoft"}
        //            }
        //        },
        //        {
        //            "To",
        //            new JArray
        //            {
        //                new JObject
        //                {
        //                    {"Email", subscriber.Email},
        //                    {"Name", subscriber.Name}
        //                }
        //            }
        //        },
        //        {"Subject", "Subscription Confirmation"},
        //        {"HTMLPart", $@"<p>Hello {subscriber.Name},</p>
        //                        <p>Thank you for subscribing!</p>
        //                        <p>To unsubscribe, click <a href='{confirmationLink}'>here</a>.</p>"
        //        }
        //    }
        //    });

        //    try
        //    {
        //        var response = await _mailjetClient.PostAsync(request);
        //        if (!response.IsSuccessStatusCode)
        //        {
        //            // Handle error if sending email fails
        //            // For example, log the error or take appropriate action
        //            // You can throw an exception if needed
        //            throw new Exception("Failed to send confirmation email");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log the exception or take appropriate action
        //        // For example:
        //        // _logger.LogError(ex, "Failed to send confirmation email");
        //    }
        //}
    }
}

