using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Hubs;
using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly WebPushNotificationService _webPushNotificationService;

        public NotificationService(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hubContext,
            UserManager<ApplicationUser> userManager,
            WebPushNotificationService webPushNotificationService)
        {
            _context = context;
            _hubContext = hubContext;
            _userManager = userManager;
            _webPushNotificationService = webPushNotificationService;
        }


        // =====================================================
        // CREATE NOTIFICATION FOR ONE USER
        // Database + SignalR + Web Push
        // =====================================================

        public async Task<Notification> CreateAsync(
            string userId,
            string title,
            string message,
            string type = "General",
            string? relatedUrl = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                RelatedUrl = relatedUrl,
                IsRead = false,
                CreatedAt = DateTime.Now
            };


            // =============================================
            // SAVE TO DATABASE
            // =============================================

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();


            // =============================================
            // CURRENT UNREAD COUNT
            // =============================================

            var unreadCount =
                await GetUnreadCountAsync(userId);


            // =============================================
            // SIGNALR REAL-TIME NOTIFICATION
            // Used while Serish AutoCare is open.
            // =============================================

            await _hubContext.Clients
                .Group($"NotificationUser_{userId}")
                .SendAsync(
                    "ReceiveNotification",
                    new
                    {
                        notificationId =
                            notification.NotificationId,

                        title =
                            notification.Title,

                        message =
                            notification.Message,

                        type =
                            notification.Type,

                        relatedUrl =
                            notification.RelatedUrl,

                        createdAt =
                            notification.CreatedAt,

                        unreadCount =
                            unreadCount
                    });


            // =============================================
            // WEB PUSH NOTIFICATION
            // Used for registered phones/browsers,
            // including when the PWA is in background.
            // =============================================

            try
            {
                await _webPushNotificationService
                    .SendToUserAsync(
                        userId,
                        notification.Title,
                        notification.Message,
                        notification.RelatedUrl
                    );
            }
            catch (Exception)
            {
                // Web Push must never prevent the normal
                // Serish AutoCare notification from working.
                //
                // Detailed Web Push errors are already
                // handled/logged inside
                // WebPushNotificationService.
            }


            return notification;
        }


        // =====================================================
        // CREATE NOTIFICATION FOR MULTIPLE USERS
        // =====================================================

        public async Task CreateForUsersAsync(
            IEnumerable<string> userIds,
            string title,
            string message,
            string type = "General",
            string? relatedUrl = null)
        {
            var uniqueUserIds = userIds
                .Where(id =>
                    !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();


            foreach (var userId in uniqueUserIds)
            {
                await CreateAsync(
                    userId,
                    title,
                    message,
                    type,
                    relatedUrl
                );
            }
        }


        // =====================================================
        // CREATE NOTIFICATION FOR ROLE
        // Example: notify every Admin
        // =====================================================

        public async Task CreateForRoleAsync(
            string role,
            string title,
            string message,
            string type = "General",
            string? relatedUrl = null)
        {
            var users =
                await _userManager
                    .GetUsersInRoleAsync(role);


            var activeUserIds = users
                .Where(u => u.IsActive)
                .Select(u => u.Id)
                .ToList();


            await CreateForUsersAsync(
                activeUserIds,
                title,
                message,
                type,
                relatedUrl
            );
        }


        // =====================================================
        // GET USER NOTIFICATIONS
        // =====================================================

        public async Task<List<Notification>>
            GetUserNotificationsAsync(
                string userId,
                int take = 20)
        {
            return await _context.Notifications
                .Where(n =>
                    n.UserId == userId)
                .OrderByDescending(n =>
                    n.CreatedAt)
                .Take(take)
                .ToListAsync();
        }


        // =====================================================
        // GET UNREAD COUNT
        // =====================================================

        public async Task<int> GetUnreadCountAsync(
            string userId)
        {
            return await _context.Notifications
                .CountAsync(n =>
                    n.UserId == userId &&
                    !n.IsRead);
        }


        // =====================================================
        // MARK ONE AS READ
        // =====================================================

        public async Task<bool> MarkAsReadAsync(
            int notificationId,
            string userId)
        {
            var notification =
                await _context.Notifications
                    .FirstOrDefaultAsync(n =>
                        n.NotificationId ==
                            notificationId &&
                        n.UserId ==
                            userId);


            if (notification == null)
            {
                return false;
            }


            if (!notification.IsRead)
            {
                notification.IsRead = true;

                await _context.SaveChangesAsync();
            }


            await SendUnreadCountAsync(userId);

            return true;
        }


        // =====================================================
        // MARK ALL AS READ
        // =====================================================

        public async Task MarkAllAsReadAsync(
            string userId)
        {
            var unreadNotifications =
                await _context.Notifications
                    .Where(n =>
                        n.UserId == userId &&
                        !n.IsRead)
                    .ToListAsync();


            if (unreadNotifications.Any())
            {
                foreach (
                    var notification
                    in unreadNotifications)
                {
                    notification.IsRead = true;
                }


                await _context.SaveChangesAsync();
            }


            await SendUnreadCountAsync(userId);
        }


        // =====================================================
        // SEND UPDATED BADGE COUNT
        // =====================================================

        private async Task SendUnreadCountAsync(
            string userId)
        {
            var unreadCount =
                await GetUnreadCountAsync(userId);


            await _hubContext.Clients
                .Group(
                    $"NotificationUser_{userId}")
                .SendAsync(
                    "NotificationCountUpdated",
                    unreadCount
                );
        }
    }
}