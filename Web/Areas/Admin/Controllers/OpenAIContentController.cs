using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Web.Areas.Admin.Controllers
{
    public class OpenAIContentController : Controller
    {
        private readonly IConfiguration _configuration;

        public OpenAIContentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        [HttpGet]
        public IActionResult GenerateContent()
        {
            return View();
        }

        // GET: /YourController/GenerateContent
        [HttpPost]
        public async Task<IActionResult> GenerateContent([FromBody] GenerateContentRequest request)
        {
            try
            {
                // Retrieve the input text from the request
                string inputText = request.InputText;

                // Validate input text (optional)
                if (string.IsNullOrWhiteSpace(inputText))
                {
                    return BadRequest("Input text cannot be empty.");
                }

                // Retrieve OpenAI API key from configuration
                string apiKey = _configuration["OpenAI:ApiKey"];

                // Call OpenAI API to generate content using the input text
                string generatedContent = await GenerateContentWithOpenAI(apiKey, inputText);

                // Return the generated content
                return Ok(generatedContent);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine(ex.Message);
                // Return an error response
                return StatusCode(500, "Error occurred while generating content.");
            }
        }

        // Method to generate content using OpenAI API
        private async Task<string> GenerateContentWithOpenAI(string apiKey, string inputText)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    // Set up HTTP client and request
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                    // Prepare request body
                    var requestBody = new
                    {
                        model = "babbage-002",
                        prompt = inputText,
                        max_tokens = 100
                        
                    };

                    // Serialize request body to JSON
                    var jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);

                    // Make HTTP POST request to OpenAI API
                    var response = await httpClient.PostAsync("https://api.openai.com/v1/completions", new StringContent(jsonContent, Encoding.UTF8, "application/json"));

                    // Check if request was successful
                    //response.EnsureSuccessStatusCode();

                    // Read response content
                    string responseBody = await response.Content.ReadAsStringAsync();

                    // Return generated content
                    return responseBody;
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine(ex.Message);
                throw;
            }
        }



        // Model class to represent request body
        public class GenerateContentRequest
        {
            public string InputText { get; set; }
        }
    }
}
