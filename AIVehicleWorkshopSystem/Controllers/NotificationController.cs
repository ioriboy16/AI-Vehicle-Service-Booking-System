using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using AIVehicleWorkshopSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly NotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public NotificationController(
            NotificationService notificationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _notificationService = notificationService;
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
        }


        // =========================
        // NOTIFICATION PAGE
        // =========================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var notifications =
                await _notificationService
                    .GetUserNotificationsAsync(userId, 100);

            return View(notifications);
        }


        // =========================
        // GET RECENT NOTIFICATIONS
        // =========================

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var notifications =
                await _notificationService
                    .GetUserNotificationsAsync(userId, 20);

            var unreadCount =
                await _notificationService
                    .GetUnreadCountAsync(userId);

            return Json(new
            {
                unreadCount,

                notifications = notifications.Select(n => new
                {
                    notificationId = n.NotificationId,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type,
                    relatedUrl = n.RelatedUrl,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt
                })
            });
        }


        // =========================
        // GET UNREAD COUNT
        // =========================

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var count =
                await _notificationService
                    .GetUnreadCountAsync(userId);

            return Json(new
            {
                unreadCount = count
            });
        }


        // =========================
        // MARK ONE AS READ
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(
            int notificationId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var success =
                await _notificationService.MarkAsReadAsync(
                    notificationId,
                    userId);

            if (!success)
            {
                return NotFound();
            }

            return Ok();
        }


        // =========================
        // MARK ALL AS READ
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _notificationService
                .MarkAllAsReadAsync(userId);

            return Ok(new
            {
                success = true
            });
        }


        // =========================
        // OPEN NOTIFICATION
        // =========================

        [HttpGet]
        public async Task<IActionResult> Open(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var notifications =
                await _notificationService
                    .GetUserNotificationsAsync(userId, 100);

            var notification =
                notifications.FirstOrDefault(n =>
                    n.NotificationId == id);

            if (notification == null)
            {
                return NotFound();
            }

            await _notificationService.MarkAsReadAsync(
                notification.NotificationId,
                userId);

            if (string.IsNullOrWhiteSpace(
                notification.RelatedUrl))
            {
                return RedirectToAction(nameof(Index));
            }

            return LocalRedirect(
                notification.RelatedUrl);
        }


        // =============================================
        // GET VAPID PUBLIC KEY
        // Safe to send to the browser.
        // NEVER send the private key.
        // =============================================

        [HttpGet]
        public IActionResult GetVapidPublicKey()
        {
            var publicKey =
                _configuration["Vapid:PublicKey"];

            if (string.IsNullOrWhiteSpace(publicKey))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "VAPID public key is not configured."
                    });
            }

            return Json(new
            {
                publicKey
            });
        }


        // =============================================
        // SAVE PUSH SUBSCRIPTION
        // Called after the user allows notifications
        // on their browser / installed PWA.
        // =============================================

        [HttpPost]
        public async Task<IActionResult> Subscribe(
            [FromBody] PushSubscriptionRequest request)
        {
            var userId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (request == null ||
                string.IsNullOrWhiteSpace(request.Endpoint) ||
                request.Keys == null ||
                string.IsNullOrWhiteSpace(request.Keys.P256dh) ||
                string.IsNullOrWhiteSpace(request.Keys.Auth))
            {
                return BadRequest(new
                {
                    message =
                        "Invalid push subscription."
                });
            }


            // Check whether this browser/device
            // has already been registered.
            var existingSubscription =
                await _context.PushSubscriptions
                    .FirstOrDefaultAsync(p =>
                        p.Endpoint == request.Endpoint);


            if (existingSubscription != null)
            {
                // Update subscription in case
                // browser keys have changed.
                existingSubscription.UserId =
                    userId;

                existingSubscription.P256dh =
                    request.Keys.P256dh;

                existingSubscription.Auth =
                    request.Keys.Auth;

                existingSubscription.CreatedAt =
                    DateTime.UtcNow;
            }
            else
            {
                var subscription =
                    new PushSubscription
                    {
                        UserId = userId,
                        Endpoint = request.Endpoint,
                        P256dh = request.Keys.P256dh,
                        Auth = request.Keys.Auth,
                        CreatedAt = DateTime.UtcNow
                    };

                _context.PushSubscriptions.Add(
                    subscription);
            }


            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message =
                    "Push notifications enabled."
            });
        }
    }


    // =============================================
    // REQUEST MODEL FROM BROWSER PUSH API
    // =============================================

    public class PushSubscriptionRequest
    {
        public string Endpoint { get; set; }
            = string.Empty;

        public PushSubscriptionKeys Keys { get; set; }
            = new();
    }


    public class PushSubscriptionKeys
    {
        public string P256dh { get; set; }
            = string.Empty;

        public string Auth { get; set; }
            = string.Empty;
    }
}