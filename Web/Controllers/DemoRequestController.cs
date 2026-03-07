using Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Model;
using Services.EmailSend;
using Services.Interaces;
using System.Text;

namespace Web.Controllers
{
    public class DemoRequestController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SurveyContext _context;
        private readonly IEmailServices _emailService;

        public DemoRequestController(UserManager<ApplicationUser> userManager, SurveyContext context, IEmailServices emailService)
        {
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
        }

        // Danish Demo Endpoint
        [HttpPost]
        public async Task<IActionResult> RequestDemo([FromBody] DemoRequest request)
        {
            return await CreateDemoAccount(request, "da");
        }

        // English Demo Endpoint
        [HttpPost]
        public async Task<IActionResult> RequestDemoEN([FromBody] DemoRequest request)
        {
            return await CreateDemoAccount(request, "en");
        }

        // Shared logic for both languages
        private async Task<IActionResult> CreateDemoAccount(DemoRequest request, string language)
        {
            try
            {
                // Validate email
                if (string.IsNullOrEmpty(request.Email))
                {
                    var errorMsg = language == "da" ? "Email er påkrævet" : "Email is required";
                    return Json(new { success = false, message = errorMsg });
                }

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    var existsMsg = language == "da" ? "Email allerede i brug" : "Email already in use";
                    return Json(new { success = false, message = existsMsg });
                }

                // Generate password
                string demoPassword = GeneratePassword();

                // Create demo user
                var demoUser = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    EmailConfirmed = true,
                    FirstName = "Demo",
                    LastName = "User"
                };

                // Create user
                var result = await _userManager.CreateAsync(demoUser, demoPassword);

                if (result.Succeeded)
                {
                    // Assign Demo role
                    await _userManager.AddToRoleAsync(demoUser, "Demo");

                    // Send email with credentials (language-specific)
                    bool emailSent = await SendDemoCredentialsEmail(demoUser.Email, demoPassword, language);

                    if (emailSent)
                    {
                        var successMsg = language == "da"
                            ? "Demo konto oprettet! Check din email for login detaljer."
                            : "Demo account created! Check your email for login details.";
                        return Json(new { success = true, message = successMsg });
                    }
                    else
                    {
                        var emailErrorMsg = language == "da"
                            ? "Demo konto oprettet, men email kunne ikke sendes. Kontakt support."
                            : "Demo account created, but email could not be sent. Please contact support.";
                        return Json(new { success = true, message = emailErrorMsg });
                    }
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    var userErrorMsg = language == "da"
                        ? "Fejl ved oprettelse af bruger: " + errors
                        : "Error creating user: " + errors;
                    return Json(new { success = false, message = userErrorMsg });
                }
            }
            catch (Exception ex)
            {
                var exceptionMsg = language == "da" ? "Fejl: " + ex.Message : "Error: " + ex.Message;
                return Json(new { success = false, message = exceptionMsg });
            }
        }

        // Language-aware email sending
        private async Task<bool> SendDemoCredentialsEmail(string email, string password, string language)
        {
            if (language == "da")
            {
                return await SendDanishDemoEmail(email, password);
            }
            else
            {
                return await SendEnglishDemoEmail(email, password);
            }
        }

        // Danish email template
        private async Task<bool> SendDanishDemoEmail(string email, string password)
        {
            var subject = "🚀 Din A-Survey Demo Konto er Klar!";

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ 
            font-family: Arial, sans-serif; 
            line-height: 1.6; 
            color: #333; 
            margin: 0; 
            padding: 0; 
        }}
        .container {{ 
            max-width: 600px; 
            margin: 0 auto; 
            padding: 20px; 
        }}
        .header {{ 
            background: linear-gradient(135deg, #6441a5, #2a9d8f); 
            color: white; 
            padding: 30px; 
            text-align: center; 
            border-radius: 10px 10px 0 0; 
        }}
        .content {{ 
            background: #f9f9f9; 
            padding: 30px; 
            border-radius: 0 0 10px 10px; 
        }}
        .credentials {{ 
            background: white; 
            padding: 20px; 
            border-radius: 8px; 
            margin: 20px 0; 
            border-left: 4px solid #3498db; 
        }}
        .btn {{ 
            display: inline-block; 
            background: linear-gradient(135deg, #00d2ff, #3a7bd5); 
            color: white; 
            padding: 15px 30px; 
            text-decoration: none; 
            border-radius: 8px; 
            font-weight: bold; 
            margin: 20px 0; 
        }}
        .warning {{ 
            background: #fff3cd; 
            border: 1px solid #ffeaa7; 
            padding: 15px; 
            border-radius: 8px; 
            margin: 20px 0; 
        }}
        .footer {{ 
            text-align: center; 
            color: #666; 
            margin-top: 30px; 
            border-top: 1px solid #ddd; 
            padding-top: 20px; 
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎯 Velkommen til A-Survey Demo!</h1>
            <p>Din demo konto er klar til brug med fulde AI-funktioner</p>
        </div>
        
        <div class='content'>
            <h2>🔐 Login Detaljer</h2>
            
            <div class='credentials'>
                <p><strong>📧 Email:</strong> {email}</p>
                <p><strong>🔑 Adgangskode:</strong> <code style='background: #f4f4f4; padding: 5px; border-radius: 4px; font-family: monospace; font-weight: bold; color: #e74c3c;'>{password}</code></p>
            </div>
            
            <div style='text-align: center; margin: 30px 0;'>
                <a href='https://unabused-terina-wavier.ngrok-free.dev/Account/Login' class='btn'>
                    🚀 Log Ind til A-Survey
                </a>
            </div>
            
            <div class='warning'>
                <h3>📅 Demo Information:</h3>
                <ul style='margin: 10px 0; padding-left: 20px;'>
                    <li>✨ <strong>Demo periode:</strong> 14 dage fra i dag</li>
                    <li>🧠 <strong>AI-funktioner:</strong> Alle tilgængelige</li>
                    <li>📊 <strong>Analytics:</strong> Komplet dashboard inkluderet</li>
                    <li>📋 <strong>Surveys:</strong> Ubegrænset oprettelse</li>
                    <li>🔄 <strong>Opgradering:</strong> Kan opgraderes til fuld version</li>
                </ul>
            </div>

            <div style='background: #e8f4fd; border: 1px solid #bee5eb; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                <h3>💡 Kom Hurtigt i Gang:</h3>
                <ol style='margin: 10px 0; padding-left: 20px;'>
                    <li>Klik på login linket ovenfor</li>
                    <li>Indtast din email og adgangskode</li>
                    <li>Udforsk de forudindlæste demo surveys</li>
                    <li>Test AI-analyse funktionerne</li>
                    <li>Opret dine egne surveys</li>
                </ol>
            </div>
            
            <p><strong>Har du spørgsmål eller brug for hjælp?</strong><br>
            Kontakt os på: <a href='mailto:info@seosoft.dk'>info@seosoft.dk</a><br>
            Telefon: +45 61 77 73 36</p>
        </div>

        <div class='footer'>
            <p>
                Med venlig hilsen<br>
                <strong>A-Survey Team</strong><br>
                SeoSoft ApS<br>
                <small>Intelligent Survey Platform med AI-indsigter</small>
            </p>
        </div>
    </div>
</body>
</html>";

            try
            {
                var emailToSend = new EmailToSend(email, subject, htmlBody);
                return await _emailService.SendConfirmationEmailAsync(emailToSend);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        // English email template
        private async Task<bool> SendEnglishDemoEmail(string email, string password)
        {
            var subject = "🚀 Your A-Survey Demo Account is Ready!";

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ 
            font-family: Arial, sans-serif; 
            line-height: 1.6; 
            color: #333; 
            margin: 0; 
            padding: 0; 
        }}
        .container {{ 
            max-width: 600px; 
            margin: 0 auto; 
            padding: 20px; 
        }}
        .header {{ 
            background: linear-gradient(135deg, #6441a5, #2a9d8f); 
            color: white; 
            padding: 30px; 
            text-align: center; 
            border-radius: 10px 10px 0 0; 
        }}
        .content {{ 
            background: #f9f9f9; 
            padding: 30px; 
            border-radius: 0 0 10px 10px; 
        }}
        .credentials {{ 
            background: white; 
            padding: 20px; 
            border-radius: 8px; 
            margin: 20px 0; 
            border-left: 4px solid #3498db; 
        }}
        .btn {{ 
            display: inline-block; 
            background: linear-gradient(135deg, #00d2ff, #3a7bd5); 
            color: white; 
            padding: 15px 30px; 
            text-decoration: none; 
            border-radius: 8px; 
            font-weight: bold; 
            margin: 20px 0; 
        }}
        .warning {{ 
            background: #fff3cd; 
            border: 1px solid #ffeaa7; 
            padding: 15px; 
            border-radius: 8px; 
            margin: 20px 0; 
        }}
        .footer {{ 
            text-align: center; 
            color: #666; 
            margin-top: 30px; 
            border-top: 1px solid #ddd; 
            padding-top: 20px; 
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎯 Welcome to A-Survey Demo!</h1>
            <p>Your demo account is ready with full AI capabilities</p>
        </div>
        
        <div class='content'>
            <h2>🔐 Login Credentials</h2>
            
            <div class='credentials'>
                <p><strong>📧 Email:</strong> {email}</p>
                <p><strong>🔑 Password:</strong> <code style='background: #f4f4f4; padding: 5px; border-radius: 4px; font-family: monospace; font-weight: bold; color: #e74c3c;'>{password}</code></p>
            </div>
            
            <div style='text-align: center; margin: 30px 0;'>
                <a href='https://18fd-2a0d-e487-311f-699e-4819-e39c-a8df-8179.ngrok-free.app/Account/Login' class='btn'>
                    🚀 Login to A-Survey
                </a>
            </div>
            
            <div class='warning'>
                <h3>📅 Demo Information:</h3>
                <ul style='margin: 10px 0; padding-left: 20px;'>
                    <li>✨ <strong>Demo period:</strong> 14 days from today</li>
                    <li>🧠 <strong>AI features:</strong> All available</li>
                    <li>📊 <strong>Analytics:</strong> Complete dashboard included</li>
                    <li>📋 <strong>Surveys:</strong> Unlimited creation</li>
                    <li>🔄 <strong>Upgrade:</strong> Convert to full version anytime</li>
                </ul>
            </div>

            <div style='background: #e8f4fd; border: 1px solid #bee5eb; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                <h3>💡 Quick Start Guide:</h3>
                <ol style='margin: 10px 0; padding-left: 20px;'>
                    <li>Click the login button above</li>
                    <li>Enter your email and password</li>
                    <li>Explore the pre-loaded demo surveys</li>
                    <li>Test the AI analysis features</li>
                    <li>Create your own surveys</li>
                </ol>
            </div>
            
            <p><strong>Questions or need help?</strong><br>
            Contact us at: <a href='mailto:info@seosoft.dk'>info@seosoft.dk</a><br>
            Phone: +45 61 77 73 36</p>
        </div>

        <div class='footer'>
            <p>
                Best regards,<br>
                <strong>A-Survey Team</strong><br>
                SeoSoft ApS<br>
                <small>Intelligent Survey Platform with AI Insights</small>
            </p>
        </div>
    </div>
</body>
</html>";

            try
            {
                var emailToSend = new EmailToSend(email, subject, htmlBody);
                return await _emailService.SendConfirmationEmailAsync(emailToSend);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private string GeneratePassword()
        {
            var random = new Random();

            // Ensure password meets all ASP.NET Identity requirements
            var digits = "0123456789";
            var lowercase = "abcdefghijklmnopqrstuvwxyz";
            var uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var special = "!@#$%";

            // Guarantee at least one of each required type
            var password = new StringBuilder();
            password.Append(digits[random.Next(digits.Length)]);        // At least 1 digit
            password.Append(lowercase[random.Next(lowercase.Length)]);  // At least 1 lowercase
            password.Append(uppercase[random.Next(uppercase.Length)]);  // At least 1 uppercase
            password.Append(special[random.Next(special.Length)]);      // At least 1 special char

            // Fill the rest with random characters
            var allChars = digits + lowercase + uppercase + special;
            for (int i = 0; i < 4; i++) // Add 4 more random chars
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password to randomize position of guaranteed chars
            var passwordArray = password.ToString().ToCharArray();
            for (int i = passwordArray.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (passwordArray[i], passwordArray[j]) = (passwordArray[j], passwordArray[i]);
            }

            return $"Demo{new string(passwordArray)}";
        }
    }

    public class DemoRequest
    {
        public string Email { get; set; }
    }
}