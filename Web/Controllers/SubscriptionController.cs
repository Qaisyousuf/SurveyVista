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

            if (ModelState.IsValid)
            {
                try
                {
                    string email = subscription.Email;
                    string senderName = email.Substring(0, email.IndexOf('@')).ToUpper();

                    // Check if the email already exists
                    var existingSubscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Email == email);
                    if (existingSubscription != null)
                    {
                        TempData["error"] = "Du er allerede abonneret.";
                        return RedirectToAction("", "home");
                    }

                    string subject = "Bekræft dit abonnement";
                    string confirmationPath = _configuration["Email:ConfirmEmailPath"]; // Retrieve the confirmation path from appsettings
                    string confirmationUrl = $"{Request.Scheme}://{Request.Host}/{confirmationPath}?email={email}"; // Construct the confirmation URL
                    string unsubscribeUrl = $"{Request.Scheme}://{Request.Host}/unsubscribe?email={email}"; // Add unsubscribe URL

                    string body = $@"<!DOCTYPE html>
                                    <html>
                                    <head>
                                        <meta charset=""UTF-8"">
                                    </head>
                                    <body style=""font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: white;"">
    
                                        <p>Hej {senderName},</p>
    
                                        <p>Tak for dit abonnement!</p>
    
                                        <p>For at bekræfte dit abonnement, skal du klikke på knappen herunder:</p>
    
                                        <p>
                                            <a href=""{confirmationUrl}"" style=""display: inline-block; padding: 12px 24px; background-color: #28a745; color: white; text-decoration: none; border-radius: 3px; font-weight: bold;"">
                                                Bekræft abonnement
                                            </a>
                                        </p>
    
                                        <p>Hvis knappen ikke virker, kan du kopiere dette link ind i din browser:</p>
                                        <p>{confirmationUrl}</p>
    
    
                                        <br>
    
                                        <p>Med venlig hilsen,<br>
                                        Nærværskonsulenterne ApS</p>
    
                                        <hr style=""border: none; border-top: 1px solid #cccccc; margin: 20px 0;"">
    
                                        <p style=""font-size: 12px; color: #666666;"">
                                            Nærværskonsulenterne ApS<br>
                                            Brødemosevej 24A, 3300 Frederiksværk<br>
                                            kontakt@nvkn.dk
                                        </p>
    
                                      
    
                                    </body>
                                    </html>";

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
                    TempData["success"] = "Abonnement oprettet. Bekræft venligst din email.";
                    return RedirectToAction("", "home");
                }
                catch (Exception)
                {
                    TempData["error"] = "Kunne ikke oprette abonnement.";
                    return RedirectToAction("", "home");
                }
            }

            return RedirectToAction("", "home");
        }

        [HttpGet]
        public async Task<IActionResult> Confirmation(string email)
        {
            try
            {
                // Find the subscription with the provided email
                var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Email == email);

                if (subscription != null)
                {
                    if (subscription.IsSubscribed)
                    {
                        // If IsSubscribed is already true, inform the user that the email is already confirmed
                        ViewBag.Message = "Din email er allerede bekræftet. Tak!";
                    }
                    else
                    {
                        // Update the IsSubscribed property to true
                        subscription.IsSubscribed = true;
                        _context.Subscriptions.Update(subscription);

                        var sentEmails = _context.SentNewsletterEamils.Where(e => e.RecipientEmail == email);

                        // Set IsUnsubscribed flag to true for email events
                        foreach (var emailEvent in sentEmails)
                        {
                            emailEvent.IsUnsubscribed = false;
                            _context.Entry(emailEvent).State = EntityState.Modified;
                        }

                        await _context.SaveChangesAsync();

                        // Send a "thank you" email to the user
                        string subject = "Tak for bekræftelse af dit abonnement";
                        string unsubscribeUrl = $"{Request.Scheme}://{Request.Host}/unsubscribe?email={email}";

                        string body = $@"<!DOCTYPE html>
                                        <html>
                                        <head>
                                            <meta charset=""UTF-8"">
                                        </head>
                                        <body style=""font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: white;"">
    
                                            <p>Hej {subscription.Name},</p>
    
                                            <p>Tak for at bekræfte dit abonnement. Du er nu tilmeldt vores nyhedsbrev.</p>
    
                                            <p>Du vil modtage vores seneste nyheder og opdateringer på denne email-adresse.</p>
    
                                        
    
                                            <br>
    
                                            <p>Med venlig hilsen,<br>
                                            Nærværskonsulenterne ApS</p>
    
                                            <hr style=""border: none; border-top: 1px solid #cccccc; margin: 20px 0;"">
    
                                            <p style=""font-size: 12px; color: #666666;"">
                                                Nærværskonsulenterne ApS<br>
                                                Brødemosevej 24A, 3300 Frederiksværk<br>
                                                kontakt@nvkn.dk
                                            </p>
    
                                          
    
                                        </body>
                                        </html>";

                        var thankYouEmail = new EmailToSend(subscription.Email, subject, body);
                        await _mailSerivces.SendConfirmationEmailAsync(thankYouEmail);

                        // Inform the user that the email has been confirmed
                        ViewBag.Message = "Tak for at bekræfte din email. Du er nu tilmeldt!";
                    }

                    return View(subscription); // You can return a view to show a confirmation message
                }
                else
                {
                    ViewBag.Message = "Du er blevet afmeldt vores nyhedsbrev. Tak!";
                    return View(subscription);
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                return View("Error"); // You can return a view to show an error message
            }
        }

        [HttpGet]
        public async Task<IActionResult> UnsubscribeConfirmation(string email)
        {
            try
            {
                // Find the subscription with the provided email
                var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Email == email);

                if (subscription != null)
                {
                    if (subscription.IsSubscribed)
                    {
                        // Update the IsSubscribed property to false
                        subscription.IsSubscribed = false;
                        _context.Subscriptions.Remove(subscription);
                        await _context.SaveChangesAsync();

                        // Remove the email from SentNewsletterEmail
                        var sentEmails = _context.SentNewsletterEamils.Where(e => e.RecipientEmail == email);

                        // Set IsUnsubscribed flag to true for email events
                        foreach (var emailEvent in sentEmails)
                        {
                            emailEvent.IsUnsubscribed = true;
                            _context.Entry(emailEvent).State = EntityState.Modified;
                        }

                        await _context.SaveChangesAsync();

                        // Inform the user that the email has been unsubscribed
                        ViewBag.Message = "Du er nu afmeldt vores nyhedsbrev. Vi er kede af at se dig gå.";

                        // Send a simple confirmation email to the user
                        string subject = "Bekræftelse på afmelding";

                        // Simple plain text email body
                        string body = $@"Hej,<br/><br/>

                          Du er nu afmeldt vores nyhedsbrev.<br/><br/>

                        Med venlig hilsen,<br>
                        Nærværskonsulenterne ApS
                        <hr><br/>

                        <br/>
                        Nærværskonsulenterne ApS<br>
                        Brødemosevej 24A, 3300 Frederiksværk<br>
                        kontakt@nvkn.dk";

                        var confirmationEmail = new EmailToSend(subscription.Email, subject, body);
                        await _mailSerivces.SendConfirmationEmailAsync(confirmationEmail);

                        return View(subscription);
                    }
                    else
                    {
                        // If IsSubscribed is already false, inform the user that the email is already unsubscribed
                        ViewBag.Message = "Din email er allerede afmeldt. Tak!";
                        return View(subscription);
                    }
                }
                else
                {
                    // Inform the user that the unsubscription process couldn't be completed
                    ViewBag.Message = "Du er blevet afmeldt vores nyhedsbrev. Tilmeld dig først.";
                    return View(subscription);
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                return View("Error");
            }
        }


        //[HttpGet]
        //public async Task<IActionResult> UnsubscribeConfirmation(string email)
        //{
        //    try
        //    {
        //        // Find the subscription with the provided email
        //        var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Email == email);

        //        if (subscription != null)
        //        {
        //            if (subscription.IsSubscribed)
        //            {
        //                // Update the IsSubscribed property to false
        //                subscription.IsSubscribed = false;
        //                _context.Subscriptions.Remove(subscription);
        //                await _context.SaveChangesAsync();

        //                // Inform the user that the email has been unsubscribed
        //                ViewBag.Message = "You have successfully unsubscribed from our newsletter. We're sorry to see you go";

        //                // Optionally, send an email confirmation to the user
        //                string subject = "Unsubscription Confirmation";
        //                string body = $@"<head>
        //                                    <meta charset=""UTF-8"">
        //                                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
        //                                    <title>Unsubscribe Confirmation</title>
        //                                    <style>
        //                                        body {{
        //                                            font-family: Arial, sans-serif;
        //                                            line-height: 1.6;
        //                                            margin: 0;
        //                                            padding: 0;
        //                                            background-color: #f9f9f9;
        //                                        }}
        //                                        .container {{
        //                                             max-width: 800px;
        //                                            margin: 0 auto;
        //                                            padding: 20px;
        //                                            border: 0.5px solid #ccc;
        //                                            border-radius: 5px;
        //                                            background-color: #f9f9f9;
        //                                        }}
        //                                        h5, h6 {{
        //                                            margin: 0;
        //                                        }}
        //                                        hr {{
        //                                            border: none;
        //                                            border-top: 1px solid #ccc;
        //                                            margin: 10px 0;
        //                                        }}
        //                                        a {{
        //                                            color: #007bff;
        //                                            text-decoration: none;
        //                                        }}
        //                                        a:hover {{
        //                                            text-decoration: underline;
        //                                        }}
        //                                    </style>
        //                                </head>
        //                                <body>
        //                                    <div class=""container"">
        //                                        <h3>Unsubscribe Confirmation</h3>
        //                                        <p>You have successfully unsubscribed from our newsletter. We're sorry to see you go.</p>
        //                                        <br>
        //                                         <h5><strong>Søren Eggert Lundsteen Olsen</strong></h5>
        //                                        <h5><a href=""https://www.seosoft.dk/"" target=""_blank"">SeoSoft ApS</a></h5>
        //                                        <hr>
        //                                        <h6>Hovedgaden 3<br>Jordrup<br>Kolding 6064<br>Denmark</h6>
        //                                    </div>
        //                                </body>
        //                                </html>";


        //                var thankYouEmail = new EmailToSend(subscription.Email, subject, body);
        //                await _mailSerivces.SendConfirmationEmailAsync(thankYouEmail);

        //                return View(subscription); // You can return a view to show a confirmation message
        //            }
        //            else
        //            {
        //                // If IsSubscribed is already false, inform the user that the email is already unsubscribed
        //                ViewBag.Message = "Your email is already unsubscribed. Thank you!";
        //                return View(subscription); // You can return a view to show a message
        //            }
        //        }
        //        else
        //        {
        //            // Inform the user that the unsubscription process couldn't be completed
        //            ViewBag.Message = "You have been unsubscribed from our newsletter. subscribe first.";
        //            return View(subscription); // You can return a view to show an error message
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log or handle the exception as needed
        //        return View("Error"); // You can return a view to show an error message
        //    }
        //}



    }
}

