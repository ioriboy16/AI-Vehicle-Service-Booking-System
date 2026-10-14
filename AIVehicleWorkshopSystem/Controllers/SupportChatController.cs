using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Hubs;
using AIVehicleWorkshopSystem.Models;
using AIVehicleWorkshopSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Controllers
{
    [Authorize]
    public class SupportChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<SupportChatHub> _hubContext;
        private readonly IWebHostEnvironment _environment;
        private readonly NotificationService _notificationService;

        public SupportChatController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IHubContext<SupportChatHub> hubContext,
    IWebHostEnvironment environment,
    NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _environment = environment;
            _notificationService = notificationService;
        }


        // =====================================================
        // CUSTOMER - OPEN OWN SUPPORT CHAT
        // =====================================================

        [Authorize(Roles = "Customer")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var conversation = await _context.SupportConversations
                .FirstOrDefaultAsync(c => c.CustomerId == user.Id);

            // First time opening Support Chat
            if (conversation == null)
            {
                conversation = new SupportConversation
                {
                    CustomerId = user.Id,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.SupportConversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(Conversation),
                new { id = conversation.ConversationId });
        }


        // =====================================================
        // CUSTOMER / ADMIN - OPEN CONVERSATION
        // =====================================================

        [Authorize(Roles = "Customer,Admin")]
        [HttpGet]
        public async Task<IActionResult> Conversation(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Challenge();
            }

            var conversation = await _context.SupportConversations
                .Include(c => c.Customer)
                .FirstOrDefaultAsync(c => c.ConversationId == id);

            if (conversation == null)
            {
                return NotFound();
            }

            var isAdmin =
                await _userManager.IsInRoleAsync(currentUser, "Admin");

            var isOwner =
                conversation.CustomerId == currentUser.Id;

            // Mechanic / another customer cannot access
            if (!isAdmin && !isOwner)
            {
                return Forbid();
            }


            var messages = await _context.SupportMessages
                .Where(m => m.ConversationId == id)
                .OrderBy(m => m.SentAt)
                .ToListAsync();


            // =============================================
            // MARK OTHER USERS' MESSAGES AS READ
            // =============================================

            var messageIds = messages
                .Where(m => m.SenderId != currentUser.Id)
                .Select(m => m.MessageId)
                .ToList();


            var alreadyReadIds = await _context.SupportMessageReads
                .Where(r =>
                    r.UserId == currentUser.Id &&
                    messageIds.Contains(r.MessageId))
                .Select(r => r.MessageId)
                .ToListAsync();


            var unreadMessageIds = messageIds
                .Except(alreadyReadIds)
                .ToList();


            foreach (var messageId in unreadMessageIds)
            {
                _context.SupportMessageReads.Add(
                    new SupportMessageRead
                    {
                        MessageId = messageId,
                        UserId = currentUser.Id,
                        ReadAt = DateTime.Now
                    });
            }


            if (unreadMessageIds.Any())
            {
                await _context.SaveChangesAsync();
            }


            // =============================================
            // GET SENDER INFORMATION
            // =============================================

            var senderIds = messages
                .Select(m => m.SenderId)
                .Distinct()
                .ToList();


            var senders = await _context.Users
                .Where(u => senderIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => u.FullName);


            var senderRoles = new Dictionary<string, string>();

            foreach (var senderId in senderIds)
            {
                var sender = await _userManager.FindByIdAsync(senderId);

                if (sender != null)
                {
                    var roles =
                        await _userManager.GetRolesAsync(sender);

                    senderRoles[senderId] =
                        roles.FirstOrDefault() ?? "User";
                }
            }


            ViewBag.Messages = messages;

            ViewBag.Senders = senders;

            ViewBag.SenderRoles = senderRoles;

            ViewBag.CurrentUserId = currentUser.Id;

            ViewBag.IsAdmin = isAdmin;

            return View(conversation);
        }


        // =====================================================
        // SEND MESSAGE
        // =====================================================

        [Authorize(Roles = "Customer,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(
            int conversationId,
            string? message,
            IFormFile? attachment)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Unauthorized();
            }


            var conversation = await _context.SupportConversations
                .FirstOrDefaultAsync(
                    c => c.ConversationId == conversationId);


            if (conversation == null)
            {
                return NotFound();
            }


            var isAdmin =
                await _userManager.IsInRoleAsync(currentUser, "Admin");

            var isOwner =
                conversation.CustomerId == currentUser.Id;


            if (!isAdmin && !isOwner)
            {
                return Forbid();
            }


            // Message cannot be completely empty
            if (string.IsNullOrWhiteSpace(message) &&
                attachment == null)
            {
                return BadRequest(
                    new { error = "Please enter a message or attach a file." });
            }


            string? attachmentFileName = null;
            string? attachmentPath = null;
            string? attachmentContentType = null;
            long? attachmentFileSize = null;


            // =============================================
            // ATTACHMENT
            // =============================================

            if (attachment != null)
            {
                const long maxFileSize =
                    5 * 1024 * 1024;

                if (attachment.Length > maxFileSize)
                {
                    return BadRequest(
                        new { error = "Maximum file size is 5 MB." });
                }


                var extension =
                    Path.GetExtension(attachment.FileName)
                        .ToLowerInvariant();


                var allowedExtensions =
                    new[]
                    {
                        ".jpg",
                        ".jpeg",
                        ".png",
                        ".webp",
                        ".pdf"
                    };


                var allowedContentTypes =
                    new[]
                    {
                        "image/jpeg",
                        "image/png",
                        "image/webp",
                        "application/pdf"
                    };


                if (!allowedExtensions.Contains(extension) ||
                    !allowedContentTypes.Contains(
                        attachment.ContentType.ToLowerInvariant()))
                {
                    return BadRequest(
                        new
                        {
                            error =
                                "Only JPG, PNG, WEBP and PDF files are allowed."
                        });
                }


                var uploadDirectory = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "support");


                Directory.CreateDirectory(uploadDirectory);


                var storedFileName =
                    $"{Guid.NewGuid()}{extension}";


                var fullPath = Path.Combine(
                    uploadDirectory,
                    storedFileName);


                await using (var stream =
                    new FileStream(
                        fullPath,
                        FileMode.Create))
                {
                    await attachment.CopyToAsync(stream);
                }


                attachmentFileName =
                    Path.GetFileName(attachment.FileName);

                attachmentPath =
                    $"/uploads/support/{storedFileName}";

                attachmentContentType =
                    attachment.ContentType;

                attachmentFileSize =
                    attachment.Length;
            }


            // =============================================
            // SAVE MESSAGE
            // =============================================

            var supportMessage =
                new SupportMessage
                {
                    ConversationId = conversationId,

                    SenderId = currentUser.Id,

                    Message =
                        string.IsNullOrWhiteSpace(message)
                            ? null
                            : message.Trim(),

                    AttachmentFileName =
                        attachmentFileName,

                    AttachmentPath =
                        attachmentPath,

                    AttachmentContentType =
                        attachmentContentType,

                    AttachmentFileSize =
                        attachmentFileSize,

                    SentAt = DateTime.Now
                };


            _context.SupportMessages.Add(supportMessage);


            conversation.UpdatedAt = DateTime.Now;


            await _context.SaveChangesAsync();


            // =============================================
            // SENDER ROLE
            // =============================================

            var roles =
                await _userManager.GetRolesAsync(currentUser);

            var senderRole =
                roles.FirstOrDefault() ?? "User";


            // =============================================
            // SIGNALR
            // =============================================

            await _hubContext.Clients
                .Group($"Support_{conversationId}")
                .SendAsync(
                    "ReceiveSupportMessage",
                    new
                    {
                        messageId =
                            supportMessage.MessageId,

                        senderId =
                            currentUser.Id,

                        senderName =
                            currentUser.FullName,

                        senderRole =
                            senderRole,

                        message =
                            supportMessage.Message,

                        attachmentFileName =
                            supportMessage.AttachmentFileName,

                        attachmentPath =
                            supportMessage.AttachmentPath,

                        attachmentContentType =
                            supportMessage.AttachmentContentType,

                        attachmentFileSize =
                            supportMessage.AttachmentFileSize,

                        sentAt =
                            supportMessage.SentAt
                    });

            // =============================================
            // GLOBAL SUPPORT CHAT NOTIFICATION
            // =============================================

            // Prepare notification preview
            var notificationMessage =
                !string.IsNullOrWhiteSpace(supportMessage.Message)
                    ? $"{currentUser.FullName}: {supportMessage.Message}"
                    : $"{currentUser.FullName} sent an attachment.";

            // Keep preview short
            if (notificationMessage.Length > 150)
            {
                notificationMessage =
                    notificationMessage.Substring(0, 147) + "...";
            }


            // =============================================
            // CUSTOMER -> ADMINS
            // =============================================
            if (!isAdmin)
            {
                var admins =
                    await _userManager.GetUsersInRoleAsync("Admin");

                var adminIds =
                    admins
                        .Where(a =>
                            a.IsActive &&
                            a.Id != currentUser.Id)
                        .Select(a => a.Id)
                        .Distinct()
                        .ToList();

                if (adminIds.Any())
                {
                    await _notificationService.CreateForUsersAsync(
                        adminIds,
                        "New Customer Message",
                        notificationMessage,
                        "SupportChat",
                        $"/SupportChat/Conversation/{conversationId}"
                    );
                }
            }


            // =============================================
            // ADMIN -> CUSTOMER
            // =============================================
            else
            {
                if (!string.IsNullOrEmpty(conversation.CustomerId) &&
                    conversation.CustomerId != currentUser.Id)
                {
                    await _notificationService.CreateAsync(
                        conversation.CustomerId,
                        "New Support Message",
                        notificationMessage,
                        "SupportChat",
                        $"/SupportChat/Conversation/{conversationId}"
                    );
                }
            }


            return Ok(
                new
                {
                    success = true,
                    messageId =
                        supportMessage.MessageId
                });
        }
    }
}