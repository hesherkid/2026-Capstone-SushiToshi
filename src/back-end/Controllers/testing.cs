using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace back_end.Testing
{
    [ApiController]
    [Route("api/testing")]
    public class TestingController : ControllerBase
    {
        [HttpGet("test-sendgrid")]
        public async Task<IActionResult> TestSendGrid()
        {
            try
            {
                // Replace with your actual API key for testing
                // TODO: Replace the hardcoded API key with a secure method of retrieving it, such as from environment variables or a secret manager.
                var apiKey = "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";
                var client = new SendGridClient(apiKey);

                var from = new EmailAddress("torpytim30@gmail.com", "Example User");
                var subject = "Sending with SendGrid is Fun";
                var to = new EmailAddress("tim.torpy@gmail.com", "Example User");
                var plainTextContent = "and easy to do anywhere, even with C#";
                var htmlContent = "<strong>and easy to do anywhere, even with C#</strong>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
                var response = await client.SendEmailAsync(msg);

                return Ok(new
                {
                    statusCode = response.StatusCode,
                    success = response.IsSuccessStatusCode,
                    message = response.IsSuccessStatusCode
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}