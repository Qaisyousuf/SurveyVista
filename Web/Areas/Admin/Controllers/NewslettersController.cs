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
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using System.Text.RegularExpressions;

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
        public IActionResult Index(int page = 1)
        {
            const int PageSize = 10;

            var totalSubscribedUsers = _context.Subscriptions.Count(s => s.IsSubscribed);
            ViewBag.TotalSubscribedUsers = totalSubscribedUsers;

            var totalSubscriptions = _context.Subscriptions.Count();
            var totalPages = (int)Math.Ceiling(totalSubscriptions / (double)PageSize);

            var newsLetterFromdb = _repository.GetAll()
                                              .OrderByDescending(x=>x.Id)
                                              .Skip((page - 1) * PageSize)
                                              .Take(PageSize)
                                              .ToList();

            var viewmodel = new List<NewsLetterViewModel>();

            foreach (var item in newsLetterFromdb)
            {
                viewmodel.Add(new NewsLetterViewModel
                {
                    Id = item.Id,
                    Name = item.Name,
                    Email = item.Email,
                    IsSubscribed = item.IsSubscribed
                });
            }

            var listViewModel = new PaginationViewModel
            {
                Subscriptions = viewmodel,
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(listViewModel);
        }

        public IActionResult Create()
        {
            var totalSubscribedUsers = _context.Subscriptions.Count(s => s.IsSubscribed);

            // Pass the total count to the view
            ViewBag.TotalSubscribedUsers = totalSubscribedUsers;

            return View();
        }


        [HttpPost]
        public IActionResult DeleteSelectedSubscription(List<int> selectedIds)
        {
            if (selectedIds != null && selectedIds.Any())
            {
                var subscriptions = _context.Subscriptions.Where(s => selectedIds.Contains(s.Id)).ToList();
                _context.Subscriptions.RemoveRange(subscriptions);
                _context.SaveChanges();
            }
            TempData["Success"] = "Subscriber deleted successfully";
            return RedirectToAction(nameof(Index));
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
                        string unsubscribeUrl = $"{Request.Scheme}://{Request.Host}/{confirmationPath}?email={user.Email}";

                        // This HTML version with proper line breaks works for primary inbox
                        string emailBody = $@"<!DOCTYPE html>
                                <html>
                                <head>
                                    <meta charset=""UTF-8"">
                                </head>
                                <body>
    
                                    <p>Hej {user.Name},</p>
    
                                    <div>
                                        {viewModel.Body.Replace("\n", "<br><br>")}
                                    </div>
    
                                    <p>Hvis du har spørgsmål, kan du kontakte os på kontakt@nvkn.dk</p>
    
                                    <p>Med venlig hilsen,<br/>
                                    Nærværskonsulenterne ApS</p>
    
                                    <hr>
    
                                    <p><small>
                                        Nærværskonsulenterne ApS<br/>
                                        Brødemosevej 24A, 3300 Frederiksværk<br/>
                                        kontakt@nvkn.dk
                                    </small></p>
    
                                    <p>
                                        <a href=""{unsubscribeUrl}"">
                                            <button type=""button"">Afmeld nyhedsbrev</button>
                                        </a>
                                    </p>
    
                                </body>
                                </html>";

                        var email = new EmailToSend(user.Email, viewModel.Subject, emailBody);
                        var isSent = await _emailServices.SendConfirmationEmailAsync(email);

                        // Create a record for the sent email
                        var sentEmail = new SentNewsletterEamil
                        {
                            RecipientEmail = user.Email,
                            Subject = viewModel.Subject,
                            Body = emailBody,
                            SentDate = DateTime.UtcNow,
                            IsSent = isSent
                        };
                        _context.SentNewsletterEamils.Add(sentEmail);

                        // Add small delay to look more natural
                        await Task.Delay(2000); // 2 second delay between sends
                    }

                    await _context.SaveChangesAsync();
                    TempData["success"] = "Nyhedsbrev sendt successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["error"] = "Noget gik galt: " + ex.Message;
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(viewModel);
        }
        [HttpGet]
        public IActionResult UploadSubscribers()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadSubscribers(PdfUploadViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                TempData["error"] = "Invalid model state.";
                return BadRequest("Invalid model state.");
            }

            if (viewModel.SubscriberFile == null || viewModel.SubscriberFile.Length == 0)
            {
                TempData["error"] = "No file uploaded or file is empty.";
                return BadRequest("No file uploaded or file is empty.");
            }

            var newSubscribers = new List<Subscription>();
            try
            {
                using (var pdfReader = new PdfReader(viewModel.SubscriberFile.OpenReadStream()))
                using (var pdfDocument = new PdfDocument(pdfReader))
                {
                    for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
                    {
                        var text = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page));
                        var matches = Regex.Matches(text, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.IgnoreCase);
                        foreach (Match match in matches)
                        {
                            var email = match.Value.ToLower();
                            var name = email.Split('@')[0].Replace(".", " ").Replace("_", " ");
                            if (!newSubscribers.Any(s => s.Email == email))
                            {
                                newSubscribers.Add(new Subscription { Email = email, Name = name, IsSubscribed = true });
                            }
                        }
                    }
                }

                // Optional: Check existing emails to avoid duplicates
                var existingEmails = _context.Subscriptions.Select(s => s.Email).ToHashSet();
                newSubscribers = newSubscribers.Where(s => !existingEmails.Contains(s.Email)).ToList();

                if (newSubscribers.Any())
                {
                    _context.Subscriptions.AddRange(newSubscribers);
                    await _context.SaveChangesAsync();
                    TempData["success"] = $"{newSubscribers.Count} new subscribers added successfully.";
                    return Ok($"{newSubscribers.Count} new subscribers added successfully.");
                }
                else
                {
                    TempData["info"] = "No new subscribers found in the file.";
                    return Ok("No new subscribers found in the file.");
                }
            }
            catch (Exception ex)
            {
                TempData["error"] = $"Error processing file: {ex.Message}";
                return BadRequest($"Error processing file: {ex.Message}");
            }
        }



        [HttpPost]
        public async Task<IActionResult> MailTracking()
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
