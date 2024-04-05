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

                        TempData["error"] = "Email already subscribed.";
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
                   
                    <h5>
                        Søren Eggert Lundsteen Olsen<br>
                        Seosoft ApS
                    </h5>
                        <hr>
                     <h6>
                        Hovedgaden 3 
                        Jordrup<br>
                        Kolding 6064<br>
                        Denmark
                    </6>
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
                    TempData["success"] = "Subscription successful. Please confirm your email.";
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
                        ViewBag.Message = "Your email is already confirmed. Thank you!";
                    }
                    else
                    {
                        // Update the IsSubscribed property to true
                        subscription.IsSubscribed = true;
                        _context.Subscriptions.Update(subscription);
                        await _context.SaveChangesAsync();

                        // Send a "thank you" email to the user
                        string subject = "Thank You for Confirming Your Subscription";
                        string body = $"Dear {subscription.Name},<br><br>" +
                                    "Thank you for confirming your subscription. " +
                                    "You are now subscribed to our service.<br><br>" +
                                    
                                    "<h5>Søren Eggert Lundsteen Olsen<br>Seosoft ApS</h5>" +
                                    "<hr>" +
                                    "<h6>Hovedgaden 3<br>Jordrup<br>Kolding 6064<br>Denmark</h6>";


                        var thankYouEmail = new EmailToSend(subscription.Email, subject, body);
                        await _mailSerivces.SendConfirmationEmailAsync(thankYouEmail);

                        // Inform the user that the email has been confirmed
                        ViewBag.Message = "Thank you for confirming your email. You are now subscribed!";
                    }

                    return View(subscription); // You can return a view to show a confirmation message
                }
                else
                {
                    ViewBag.Message = "You have been unsubscribed from our service. Thank you!";
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

                        // Inform the user that the email has been unsubscribed
                        ViewBag.Message = "You have been unsubscribed from our service. Thank you!";

                        // Optionally, send an email confirmation to the user
                        string subject = "Unsubscribe Confirmation";
                        string body = "You have successfully unsubscribed from our newsletter. We're sorry to see you go <br><br>" +
                              
                               
                               "<h5>Søren Eggert Lundsteen Olsen<br>Seosoft ApS</h5>"+
                                "<hr>" +
                               "<h6>Hovedgaden 3<br>Jordrup<br>Kolding 6064<br>Denmark</h6>";

                        var thankYouEmail = new EmailToSend(subscription.Email, subject, body);
                        await _mailSerivces.SendConfirmationEmailAsync(thankYouEmail);

                        return View(subscription); // You can return a view to show a confirmation message
                    }
                    else
                    {
                        // If IsSubscribed is already false, inform the user that the email is already unsubscribed
                        ViewBag.Message = "Your email is already unsubscribed. Thank you!";
                        return View(subscription); // You can return a view to show a message
                    }
                }
                else
                {
                    // Inform the user that the unsubscription process couldn't be completed
                    ViewBag.Message = "Your email does not exist in our subscription list. Please subscribe first.";
                    return View(subscription); // You can return a view to show an error message
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                return View("Error"); // You can return a view to show an error message
            }
        }



    }
}

