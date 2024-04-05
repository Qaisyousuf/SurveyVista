﻿using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TextTemplating;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
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
            if(ModelState.IsValid)
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
                            string emailBody = $"<h4>Hey {user.Name}</h4><br>{viewModel.Body}<br><h5>Søren Eggert Lundsteen Olsen<br>Seosoft ApS</h5><hr><h6>Hovedgaden 3<br>Jordrup<br>Kolding 6064<br>Denmark</h6><br/><br/><div style='text-align: center;'><a href='{confirmationUrl}' style='display: inline-block; background-color: #6c757d; color: #fff; padding: 4px 8px; text-decoration: none; border-radius: 4px;'>Unsubscribe</a></div>";

                            var email = new EmailToSend(user.Email, viewModel.Subject, emailBody);
                            var isSent = await _emailServices.SendConfirmationEmailAsync(email);

                            // Handle failure to send email if needed
                           
                        }

                        TempData["success"] = "Nesletter sent successfully.";
                        return RedirectToAction(nameof(Index));
                    
                 

                }
                catch (Exception ex)
                {
                    // Log or handle the exception as needed
                    TempData["success"] = "something went wrong.";
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(viewModel);
            
           
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
