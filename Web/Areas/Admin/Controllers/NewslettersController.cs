using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TextTemplating;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using Model;
using Newtonsoft.Json.Linq;
using OpenAI_API;
using Services.EmailSend;
using Services.Implemnetation;
using Services.Interaces;
using Web.AIConfiguration;
using Web.ViewModel.NewsLetterVM;

namespace Web.Areas.Admin.Controllers
{

  
    public class NewslettersController : Controller
    {
        private readonly INewsLetterRepository _repository;
        private readonly SurveyContext _context;
        private readonly IEmailServices _emailServices;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;

        public NewslettersController(INewsLetterRepository repository,SurveyContext context,IEmailServices emailServices,IConfiguration configuration)
        {
            _repository = repository;
            _context = context;
            _emailServices = emailServices;
            _configuration = configuration;
        
        }
        public IActionResult Index()
        {
            var totalSubscribedUsers = _context.Subscriptions.Count(s => s.IsSubscribed);

            // Pass the total count to the view
            ViewBag.TotalSubscribedUsers = totalSubscribedUsers;
            var newsLetterFromdb = _repository.GetAll();

            var viewmodel = new List<NewsLetterViewModel>();

            foreach (var item in newsLetterFromdb)
            {
                viewmodel.Add(new NewsLetterViewModel
                {
                    Id=item.Id,
                    Name=item.Name,
                    Email=item.Email,
                    IsSubscribed=item.IsSubscribed
                });
            }
            return View(viewmodel);
        }

        public IActionResult Create()
        {
            var totalSubscribedUsers = _context.Subscriptions.Count(s => s.IsSubscribed);

            // Pass the total count to the view
            ViewBag.TotalSubscribedUsers = totalSubscribedUsers;

            return View();
        }


        [HttpPost]
      
        public async Task<IActionResult> Create(SendNewsLetterViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Retrieve all subscribed users
                    var subscribedUsers = await _context.Subscriptions.Where(s => s.IsSubscribed).ToListAsync();

                    string confirmationPath = _configuration["Email:unsubscribePath"];
                    // Send the newsletter email to each subscribed user
                    foreach (var user in subscribedUsers)
                    {
                        string confirmationUrl = $"{Request.Scheme}://{Request.Host}/{confirmationPath}?email={user.Email}";
                        string emailBody = $@"<head>
                                <meta charset=""UTF-8"">
                                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                                <title>Email Confirmation</title>
                                <style>
                                    body {{
                                        font-family: Arial, sans-serif;
                                        line-height: 1.6;
                                        margin: 0;
                                        padding: 0;
                                        background-color: #f9f9f9;
                                    }}
                                    .container {{
                                          max-width: 800px;
                                            margin: 0 auto;
                                            padding: 20px;
                                            border: 0.5px solid #ccc;
                                            border-radius: 5px;
                                            background-color: #f9f9f9;
                                    }}
                                    h4, h5, h6 {{
                                        margin: 0;
                                    }}
                                    hr {{
                                        border: none;
                                        border-top: 1px solid #ccc;
                                        margin: 10px 0;
                                    }}
                                    a.button {{
                                        display: inline-block;
                                        padding: 5px 10px;
                                        background-color: #6c757d;
                                        color: #fff;
                                        text-decoration: none;
                                        border-radius: 4px;
                                    }}
                                    a.button:hover {{
                                        background-color: #5a6268;
                                    }}
                                        a {{
                                            color: #007bff;
                                            text-decoration: none;
                                        }}
                                        a:hover {{
                                            text-decoration: underline;
                                        }}
                                </style>
                            </head>
                            <body>
                                <div class=""container"">
                                    <h4>Hey {user.Name},</h4>
                                    <p>{viewModel.Body}</p><br>
                                 
                                    <h5>Søren Eggert Lundsteen Olsen</h5>
                                 <h5><a href=""https://www.seosoft.dk/"" target=""_blank"">SeoSoft ApS</a></h5>
                                 <hr>
                                    <h6>Hovedgaden 3<br>Jordrup<br>Kolding 6064<br>Denmark</h6>
                                    <div style=""text-align: center;"">
                                        <a href=""{confirmationUrl}"" class=""button"">Unsubscribe</a>
                                    </div>
                                </div>
                            </body>";

                        var email = new EmailToSend(user.Email, viewModel.Subject, emailBody);
                        var isSent = await _emailServices.SendConfirmationEmailAsync(email);

                        // Create a record for the sent email
                        var sentEmail = new SentNewsletterEamil
                        {
                            RecipientEmail = user.Email,
                            Subject = viewModel.Subject,
                            Body = emailBody,
                          
                           
                        SentDate = DateTime.UtcNow,
                            IsSent = isSent  // Assuming isSent returns a boolean indicating success
                        };
                        _context.SentNewsletterEamils.Add(sentEmail);

                       
                        // Handle failure to send email if needed
                    }

                    await _context.SaveChangesAsync();  // Save changes for all sent emails

                    TempData["success"] = "Newsletter sent successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Log or handle the exception as needed
                    TempData["error"] = "Something went wrong: " + ex.Message;
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(viewModel);
        }

       

        [HttpPost]
        public async Task<IActionResult> MailjetWebhook()
        {
            using (var reader = new StreamReader(Request.Body))
            {
                var requestBody = await reader.ReadToEndAsync();
                Console.WriteLine("Received payload: " + requestBody);
                Request.Body.Position = 0;

                try
                {
                    var events = JArray.Parse(requestBody);
                    if (events == null)
                    {
                        return BadRequest("Parsed data is null");
                    }

                    var groupedEvents = events.GroupBy(e => e.Value<string>("email"));

                    foreach (var emailGroup in groupedEvents)
                    {
                        string email = emailGroup.Key;
                        Console.WriteLine($"Processing events for email: {email}");

                        // Retrieve all matching newsletter email records for the current email
                        var newsletterEmails = await _context.SentNewsletterEamils
                            .Where(n => n.RecipientEmail == email)
                            .ToListAsync();

                        if (newsletterEmails == null || !newsletterEmails.Any())
                        {
                            Console.WriteLine("No newsletter email record found for email: " + email);
                            continue;
                        }

                        foreach (var newsletterEmail in newsletterEmails)
                        {
                            // Update the ReceivedActivity property to the current UTC time
                            newsletterEmail.ReceivedActivity = DateTime.UtcNow.ToLocalTime();
                            string ipAddress = emailGroup.First().Value<string>("ip");
                            string geolocation = emailGroup.First().Value<string>("geo");

                            // Update the newsletterEmail with IP address and geolocation
                            newsletterEmail.IpAddress = ipAddress;
                            newsletterEmail.Geo = geolocation;

                            foreach (var e in emailGroup)
                            {
                                string eventType = e.Value<string>("event");
                                Console.WriteLine($"Processing {eventType} for {email}");

                                switch (eventType)
                                {
                                    case "sent":
                                        newsletterEmail.IsDelivered = true;
                                        break;
                                    case "open":
                                        newsletterEmail.IsOpened = true;
                                        break;
                                    case "click":
                                        newsletterEmail.IsClicked = true;
                                        break;
                                    case "bounce":
                                        newsletterEmail.IsBounced = true;
                                        break;
                                    case "spam":
                                        newsletterEmail.IsSpam = true;
                                        break;
                                    case "unsub":
                                        newsletterEmail.IsUnsubscribed = true;
                                        break;
                                    case "blocked":
                                        newsletterEmail.IsBlocked = true;
                                        break;
                                    default:
                                        Console.WriteLine($"Unhandled event type: {eventType}");
                                        break;
                                }
                            }

                            _context.Entry(newsletterEmail).State = EntityState.Modified;
                        }
                    }
                    Console.WriteLine("Emails got updated ");

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception parsing JSON: " + ex.Message);
                    return BadRequest("Error parsing JSON: " + ex.Message);
                }
            }

            return Ok();







            //using (var reader = new StreamReader(Request.Body))
            //{
            //    var requestBody = await reader.ReadToEndAsync();
            //    Console.WriteLine("Received payload: " + requestBody);
            //    Request.Body.Position = 0;

            //    try
            //    {
            //        var events = JArray.Parse(requestBody);
            //        if (events == null)
            //        {
            //            return BadRequest("Parsed data is null");
            //        }

            //        foreach (JObject e in events)
            //        {
            //            string email = e.Value<string>("email");
            //            string eventType = e.Value<string>("event");
            //            Console.WriteLine($"Processing {eventType} for {email}");

            //            var newsletterEmail = await _context.SentNewsletterEamils
            //                                                .FirstOrDefaultAsync(n => n.RecipientEmail == email);

            //            if (newsletterEmail == null)
            //            {
            //                Console.WriteLine("No newsletter email record found for email: " + email);
            //                continue;
            //            }

            //            // Update the ReceivedActivity property to the current UTC time
            //            newsletterEmail.ReceivedActivity = DateTime.UtcNow.ToLocalTime();

            //            switch (eventType)
            //            {
            //                case "sent":
            //                    newsletterEmail.IsDelivered = true;
            //                    break;
            //                case "open":
            //                    newsletterEmail.IsOpened = true;
            //                    break;
            //                case "click":
            //                    newsletterEmail.IsClicked = true;
            //                    break;
            //                case "bounce":
            //                    newsletterEmail.IsBounced = true;
            //                    break;
            //                case "spam":
            //                    newsletterEmail.IsSpam = true;
            //                    break;
            //                case "unsub":
            //                    newsletterEmail.IsUnsubscribed = true;
            //                    break;
            //                case "blocked":
            //                    newsletterEmail.IsBlocked = true;
            //                    break;
            //                default:
            //                    Console.WriteLine($"Unhandled event type: {eventType}");
            //                    break;
            //            }

            //            _context.Entry(newsletterEmail).State = EntityState.Modified;
            //        }
            //        Console.WriteLine("Email got updated ");

            //        await _context.SaveChangesAsync();
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine("Exception parsing JSON: " + ex.Message);
            //        return BadRequest("Error parsing JSON: " + ex.Message);
            //    }
            //}

            //return Ok();
        }



        //public async Task<IActionResult> MailjetWebhook()
        //{
        //    using (var reader = new StreamReader(Request.Body))
        //    {
        //        var requestBody = await reader.ReadToEndAsync();
        //        Console.WriteLine("Received payload: " + requestBody);
        //        Request.Body.Position = 0;

        //        try
        //        {
        //            var events = JArray.Parse(requestBody);
        //            if (events == null)
        //            {
        //                return BadRequest("Parsed data is null");
        //            }

        //            foreach (JObject e in events)
        //            {
        //                string email = e.Value<string>("email");
        //                string eventType = e.Value<string>("event");
        //                Console.WriteLine($"Processing {eventType} for {email}");

        //                var newsletterEmail = await _context.SentNewsletterEamils
        //                                                    .FirstOrDefaultAsync(n => n.RecipientEmail == email);

        //                if (newsletterEmail == null)
        //                {
        //                    Console.WriteLine("No newsletter email record found for email: " + email);
        //                    continue;
        //                }

        //                switch (eventType)
        //                {
        //                    case "sent":
        //                        newsletterEmail.IsDelivered = true;
        //                        break;

        //                    case "open":
        //                        newsletterEmail.IsOpened = true;
        //                        break;
        //                    case "click":
        //                        newsletterEmail.IsClicked = true;
        //                        break;
        //                    case "bounce":
        //                        newsletterEmail.IsBounced = true;
        //                        break;
        //                    case "spam":
        //                        newsletterEmail.IsSpam = true;
        //                        break;
        //                    case "unsub":
        //                        newsletterEmail.IsUnsubscribed = true;
        //                        break;
        //                    case "blocked":
        //                        newsletterEmail.IsBlocked = true;
        //                        break;
        //                    default:
        //                        Console.WriteLine($"Unhandled event type: {eventType}");
        //                        break;
        //                }

        //                _context.Entry(newsletterEmail).State = EntityState.Modified;
        //            }
        //            Console.WriteLine("Email got updated ");

        //            await _context.SaveChangesAsync();
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("Exception parsing JSON: " + ex.Message);
        //            return BadRequest("Error parsing JSON: " + ex.Message);
        //        }
        //    }

        //    return Ok();
        //}



        public async Task<IActionResult> EmailStats()
        {
            var emails = await _context.SentNewsletterEamils.ToListAsync();
            return View(emails);
        }



        public async Task<IActionResult> GetEmailStatsData()
        {
            var model = await _context.SentNewsletterEamils.ToListAsync();
            return Json(model); // Returns the list of emails as JSON.
        }

        //public async Task<JsonResult> GetChartData()

        //    var emails = await _context.SentNewsletterEamils.ToListAsync();

        //    var data = new
        //    {
        //        Sent = emails.Count(e => e.IsSent),
        //        Delivered = emails.Count(e => e.IsDelivered),
        //        Opened = emails.Count(e => e.IsOpened),
        //        Clicked = emails.Count(e => e.IsClicked),
        //        Bounced = emails.Count(e => e.IsBounced),
        //        Spam = emails.Count(e => e.IsSpam),
        //        Blocked = emails.Count(e => e.IsBlocked),
        //        Unsubscribed = emails.Count(e => e.IsUnsubscribed)
        //    };

        //    return Json(data);
        //}


        [HttpPost]
        public IActionResult DeleteSelected(List<int> selectedIds)
        {
            if (selectedIds != null && selectedIds.Count > 0)
            {
                var items = _context.SentNewsletterEamils.Where(x => selectedIds.Contains(x.Id)).ToList();
                _context.SentNewsletterEamils.RemoveRange(items);
                _context.SaveChanges();
            }
            TempData["Success"] = "Email tracking result deleted successfully";
            return RedirectToAction(nameof(EmailStats));
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var newsLetterFromDb = _repository.GetById(id);

            var viewmodel = new NewsLetterViewModel
            {
                Id=newsLetterFromDb.Id,
                Email=newsLetterFromDb.Email,
                Name=newsLetterFromDb.Name,
                IsSubscribed=newsLetterFromDb.IsSubscribed
            };
            return View(viewmodel);
        }

        [HttpPost]
        [ActionName("Delete")]
        public IActionResult DeleteConfirm(int id)
        {
            _repository.Delete(id);

            _context.SaveChanges();
            TempData["Success"] = "Subscriber deleted successfully";
            return RedirectToAction(nameof(Index));
        }

       

    }
}
