using System.Net;
using System.Text.Json;
using AIVehicleWorkshopSystem.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace AIVehicleWorkshopSystem.Services
{
    public class WebPushNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebPushNotificationService> _logger;

        public WebPushNotificationService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<WebPushNotificationService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }


        // =============================================
        // SEND PUSH NOTIFICATION TO ONE USER
        // =============================================

        public async Task SendToUserAsync(
            string userId,
            string title,
            string message,
            string? url = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }


            // =========================================
            // GET VAPID CONFIGURATION
            // =========================================

            var subject =
                _configuration["Vapid:Subject"];

            var publicKey =
                _configuration["Vapid:PublicKey"];

            var privateKey =
                _configuration["Vapid:PrivateKey"];


            if (string.IsNullOrWhiteSpace(subject) ||
                string.IsNullOrWhiteSpace(publicKey) ||
                string.IsNullOrWhiteSpace(privateKey))
            {
                _logger.LogWarning(
                    "Web Push skipped because VAPID configuration is missing."
                );

                return;
            }


            // =========================================
            // GET USER DEVICES
            // =========================================

            var subscriptions =
                await _context.PushSubscriptions
                    .Where(p => p.UserId == userId)
                    .ToListAsync();


            if (subscriptions.Count == 0)
            {
                return;
            }


            // =========================================
            // CREATE PUSH PAYLOAD
            // =========================================

            var payload =
                JsonSerializer.Serialize(new
                {
                    title = string.IsNullOrWhiteSpace(title)
                        ? "Serish AutoCare"
                        : title,

                    message = message ?? string.Empty,

                    url = string.IsNullOrWhiteSpace(url)
                        ? "/"
                        : url
                });


            var vapidDetails =
                new VapidDetails(
                    subject,
                    publicKey,
                    privateKey
                );


            var webPushClient =
                new WebPushClient();


            var databaseChanged = false;


            // =========================================
            // SEND TO EVERY REGISTERED DEVICE
            // =========================================

            foreach (var savedSubscription in subscriptions)
            {
                var browserSubscription =
                    new WebPush.PushSubscription(
                        savedSubscription.Endpoint,
                        savedSubscription.P256dh,
                        savedSubscription.Auth
                    );


                try
                {
                    await webPushClient.SendNotificationAsync(
                        browserSubscription,
                        payload,
                        vapidDetails
                    );
                }
                catch (WebPushException exception)
                    when (
                        exception.StatusCode ==
                            HttpStatusCode.Gone ||

                        exception.StatusCode ==
                            HttpStatusCode.NotFound
                    )
                {
                    // Subscription is no longer valid.
                    // Example:
                    // user removed the PWA or browser
                    // invalidated the subscription.

                    _context.PushSubscriptions.Remove(
                        savedSubscription
                    );

                    databaseChanged = true;


                    _logger.LogInformation(
                        "Removed expired push subscription for user {UserId}.",
                        userId
                    );
                }
                catch (WebPushException exception)
                {
                    // Do not break normal Serish AutoCare
                    // notifications if Web Push fails.

                    _logger.LogWarning(
                        exception,
                        "Unable to send Web Push notification to user {UserId}. Status code: {StatusCode}",
                        userId,
                        exception.StatusCode
                    );
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Unexpected Web Push error for user {UserId}.",
                        userId
                    );
                }
            }


            // =========================================
            // REMOVE EXPIRED SUBSCRIPTIONS
            // =========================================

            if (databaseChanged)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}