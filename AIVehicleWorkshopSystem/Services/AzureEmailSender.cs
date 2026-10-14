using Azure;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace AIVehicleWorkshopSystem.Services
{
    public class AzureEmailSender : IEmailSender
    {
        private readonly EmailClient _emailClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureEmailSender> _logger;

        public AzureEmailSender(
            IConfiguration configuration,
            ILogger<AzureEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var connectionString =
                _configuration["AzureCommunicationServices:ConnectionString"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Azure Communication Services connection string is not configured.");
            }

            _emailClient = new EmailClient(connectionString);
        }

        public async Task SendEmailAsync(
            string email,
            string subject,
            string htmlMessage)
        {
            var senderAddress =
                _configuration["AzureCommunicationServices:SenderAddress"];

            if (string.IsNullOrWhiteSpace(senderAddress))
            {
                throw new InvalidOperationException(
                    "Azure Communication Services sender address is not configured.");
            }

            try
            {
                var operation = await _emailClient.SendAsync(
                    WaitUntil.Completed,
                    senderAddress,
                    email,
                    subject,
                    htmlMessage);

                _logger.LogInformation(
                    "Email sent successfully to {Email}. Operation ID: {OperationId}",
                    email,
                    operation.Id);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(
                    ex,
                    "Azure Communication Services failed to send email to {Email}.",
                    email);

                throw;
            }
        }
    }
}