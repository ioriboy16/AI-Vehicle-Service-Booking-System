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
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly NotificationService _notificationService;

        public ChatController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> hubContext,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _notificationService = notificationService;
        }

        // =========================
        // OPEN REPAIR CHAT
        // =========================
        [HttpGet]
        public async Task<IActionResult> RepairChat(int repairId)
        {
            var userId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // =========================
            // GET REPAIR
            // =========================
            var repair =
                await _context.Repairs
                    .Include(r => r.Appointment)
                    .ThenInclude(a => a!.Vehicle)
                    .FirstOrDefaultAsync(r =>
                        r.RepairId == repairId);

            if (repair == null)
            {
                return NotFound();
            }

            // =========================
            // ACCESS CONTROL
            // =========================
            var isAdmin =
                User.IsInRole("Admin");

            var isAssignedMechanic =
                User.IsInRole("Mechanic") &&
                repair.MechanicId == userId;

            var isRepairCustomer =
                User.IsInRole("Customer") &&
                repair.Appointment != null &&
                repair.Appointment.CustomerId == userId;

            if (!isAdmin &&
                !isAssignedMechanic &&
                !isRepairCustomer)
            {
                return Forbid();
            }

            // =========================
            // GET / CREATE CONVERSATION
            // =========================
            var conversation =
                await _context.ChatConversations
                    .FirstOrDefaultAsync(c =>
                        c.RepairId == repairId);

            if (conversation == null)
            {
                conversation =
                    new ChatConversation
                    {
                        RepairId = repairId,
                        CreatedAt = DateTime.Now
                    };

                _context.ChatConversations.Add(
                    conversation);

                await _context.SaveChangesAsync();
            }

            // =========================
            // GET MESSAGES
            // =========================
            var messages =
                await _context.ChatMessages
                    .Where(m =>
                        m.ConversationId ==
                        conversation.ConversationId)
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

            // =========================
            // MARK MESSAGES AS READ
            // =========================
            var otherUserMessageIds =
                messages
                    .Where(m =>
                        m.SenderId != userId)
                    .Select(m =>
                        m.MessageId)
                    .ToList();

            if (otherUserMessageIds.Any())
            {
                var alreadyReadMessageIds =
                    await _context.ChatMessageReads
                        .Where(r =>
                            r.UserId == userId &&
                            otherUserMessageIds.Contains(
                                r.MessageId))
                        .Select(r =>
                            r.MessageId)
                        .ToListAsync();

                var unreadMessages =
                    messages
                        .Where(m =>
                            m.SenderId != userId &&
                            !alreadyReadMessageIds.Contains(
                                m.MessageId))
                        .ToList();

                foreach (var unreadMessage in unreadMessages)
                {
                    var readRecord =
                        new ChatMessageRead
                        {
                            MessageId =
                                unreadMessage.MessageId,

                            UserId =
                                userId,

                            ReadAt =
                                DateTime.Now
                        };

                    _context.ChatMessageReads.Add(
                        readRecord);
                }

                if (unreadMessages.Any())
                {
                    await _context.SaveChangesAsync();
                }
            }

            // =========================
            // GET SENDER NAMES + ROLES
            // =========================
            var senderNames =
                new Dictionary<string, string>();

            var senderRoles =
                new Dictionary<string, string>();

            foreach (var message in messages)
            {
                if (!senderNames.ContainsKey(
                    message.SenderId))
                {
                    var sender =
                        await _userManager.FindByIdAsync(
                            message.SenderId);

                    senderNames[message.SenderId] =
                        sender?.FullName
                        ?? sender?.UserName
                        ?? "User";

                    if (sender != null)
                    {
                        var roles =
                            await _userManager
                                .GetRolesAsync(sender);

                        senderRoles[message.SenderId] =
                            roles.FirstOrDefault()
                            ?? "User";
                    }
                    else
                    {
                        senderRoles[message.SenderId] =
                            "User";
                    }
                }
            }

            // =========================
            // GET PARTICIPANTS
            // =========================
            var mechanic =
                await _userManager.FindByIdAsync(
                    repair.MechanicId);

            var customer =
                repair.Appointment == null
                    ? null
                    : await _userManager.FindByIdAsync(
                        repair.Appointment.CustomerId);

            // =========================
            // SEND DATA TO VIEW
            // =========================
            ViewBag.Repair =
                repair;

            ViewBag.Conversation =
                conversation;

            ViewBag.Messages =
                messages;

            ViewBag.SenderNames =
                senderNames;

            ViewBag.SenderRoles =
                senderRoles;

            ViewBag.CurrentUserId =
                userId;

            ViewBag.MechanicName =
                mechanic?.FullName
                ?? mechanic?.UserName
                ?? "Unknown Mechanic";

            ViewBag.CustomerName =
                customer?.FullName
                ?? customer?.UserName
                ?? "Unknown Customer";

            return View();
        }

        // =========================
        // SEND MESSAGE
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(
     int repairId,
     string? message,
     IFormFile? attachment)
        {
            var userId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            message = message?.Trim();

            var hasMessage =
                !string.IsNullOrWhiteSpace(message);

            var hasAttachment =
                attachment != null &&
                attachment.Length > 0;


            // =========================
            // EMPTY MESSAGE
            // =========================
            if (!hasMessage &&
                !hasAttachment)
            {
                TempData["ChatError"] =
                    "Please enter a message or attach a file.";

                return RedirectToAction(
                    "RepairChat",
                    new
                    {
                        repairId
                    });
            }


            // =========================
            // MESSAGE LENGTH
            // =========================
            if (hasMessage &&
                message!.Length > 2000)
            {
                TempData["ChatError"] =
                    "Message cannot exceed 2000 characters.";

                return RedirectToAction(
                    "RepairChat",
                    new
                    {
                        repairId
                    });
            }


            // =========================
            // GET REPAIR
            // =========================
            var repair =
    await _context.Repairs
        .Include(r => r.Appointment)
            .ThenInclude(a => a!.Vehicle)
        .FirstOrDefaultAsync(
            r => r.RepairId == repairId
        );

            if (repair == null)
            {
                return NotFound();
            }


            // =========================
            // AUTHORIZATION
            // =========================
            var isAdmin =
                User.IsInRole("Admin");

            var isMechanic =
     User.IsInRole("Mechanic") &&
     repair.MechanicId == userId;

            var isCustomer =
                User.IsInRole("Customer") &&
                repair.Appointment != null &&
                repair.Appointment.CustomerId == userId;

            if (!isAdmin &&
                !isMechanic &&
                !isCustomer)
            {
                return Forbid();
            }


            // =========================
            // ATTACHMENT
            // =========================
            string? attachmentPath = null;
            string? attachmentFileName = null;
            string? attachmentContentType = null;


            if (hasAttachment)
            {
                const long maxFileSize =
                    5 * 1024 * 1024;

                if (attachment!.Length > maxFileSize)
                {
                    TempData["ChatError"] =
                        "Attachment cannot exceed 5 MB.";

                    return RedirectToAction(
                        "RepairChat",
                        new
                        {
                            repairId
                        });
                }


                var extension =
                    Path.GetExtension(
                        attachment.FileName
                    ).ToLowerInvariant();


                var allowedExtensions =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp",
                ".heic",
                ".heif",
                ".pdf"
                    };


                if (!allowedExtensions.Contains(extension))
                {
                    TempData["ChatError"] =
                        "Only JPG, JPEG, PNG, WEBP, HEIC, HEIF and PDF files are allowed.";

                    return RedirectToAction(
                        "RepairChat",
                        new
                        {
                            repairId
                        });
                }


                var contentType =
                    attachment.ContentType?
                        .ToLowerInvariant()
                    ?? string.Empty;


                var allowedContentTypes =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                "image/jpeg",
                "image/png",
                "image/webp",
                "image/heic",
                "image/heif",
                "application/pdf"
                    };


                if (!allowedContentTypes.Contains(contentType))
                {
                    TempData["ChatError"] =
                        $"Unsupported file type: {contentType}";

                    return RedirectToAction(
                        "RepairChat",
                        new
                        {
                            repairId
                        });
                }


                // =========================
                // SAVE FILE
                // =========================
                var uploadDirectory =
     Path.Combine(
         Directory.GetCurrentDirectory(),
         "wwwroot",
         "uploads",
         "chat"
     );

                Directory.CreateDirectory(
                    uploadDirectory
                );


                var storedFileName =
                    $"{Guid.NewGuid():N}{extension}";


                var physicalPath =
                    Path.Combine(
                        uploadDirectory,
                        storedFileName
                    );


                await using (
                    var stream =
                        new FileStream(
                            physicalPath,
                            FileMode.Create))
                {
                    await attachment.CopyToAsync(
                        stream
                    );
                }


                attachmentPath =
                    $"/uploads/chat/{storedFileName}";

                attachmentFileName =
                    Path.GetFileName(
                        attachment.FileName
                    );

                attachmentContentType =
                    contentType;
            }


            // =========================
            // GET / CREATE CONVERSATION
            // =========================
            var conversation =
                await _context.ChatConversations
                    .FirstOrDefaultAsync(
                        c => c.RepairId == repairId
                    );


            if (conversation == null)
            {
                conversation =
                    new ChatConversation
                    {
                        RepairId = repairId,
                        CreatedAt = DateTime.UtcNow
                    };

                _context.ChatConversations.Add(
                    conversation
                );

                await _context.SaveChangesAsync();
            }


            // =========================
            // CREATE MESSAGE
            // =========================
            var chatMessage =
                new ChatMessage
                {
                    ConversationId =
                        conversation.ConversationId,

                    SenderId =
                        userId,

                    Message =
                        hasMessage
                            ? message
                            : null,

                    AttachmentPath =
                        attachmentPath,

                    AttachmentFileName =
                        attachmentFileName,

                    AttachmentContentType =
                        attachmentContentType,

                    SentAt =
                        DateTime.UtcNow
                };


            _context.ChatMessages.Add(
                chatMessage
            );

            await _context.SaveChangesAsync();


            // =========================
            // SENDER INFORMATION
            // =========================
            var sender =
                await _userManager
                    .FindByIdAsync(userId);

            var senderName =
                sender?.FullName
                ?? sender?.UserName
                ?? "User";


            var senderRole =
                "User";

            if (sender != null)
            {
                var roles =
                    await _userManager
                        .GetRolesAsync(sender);

                senderRole =
                    roles.FirstOrDefault()
                    ?? "User";
            }


            // =========================
            // SIGNALR
            // =========================
            await _hubContext
                .Clients
                .Group($"Repair-{repairId}")
                .SendAsync(
                    "ReceiveMessage",
                    new
                    {
                        messageId =
                            chatMessage.MessageId,

                        senderId =
                            userId,

                        senderName,

                        senderRole,

                        message =
                            chatMessage.Message,

                        attachmentPath =
                            chatMessage.AttachmentPath,

                        attachmentFileName =
                            chatMessage.AttachmentFileName,

                        attachmentContentType =
                            chatMessage.AttachmentContentType,

                        sentAt =
                            chatMessage.SentAt
                                .ToString(
                                    "dd MMM yyyy, hh:mm tt"
                                )
                    }
                );


            // =========================
            // NOTIFICATION
            // =========================
            string? recipientUserId = null;


            if (isCustomer)
            {
                recipientUserId =
                    repair.MechanicId;
            }
            else if (isMechanic)
            {
                recipientUserId =
                    repair.Appointment?.CustomerId;
            }


            if (!string.IsNullOrEmpty(
                recipientUserId))
            {
                var notificationMessage =
                    hasAttachment
                        ? $"{senderName} sent you an attachment in Repair #{repairId}."
                        : $"{senderName} sent you a message in Repair #{repairId}.";


                await _notificationService
    .CreateAsync(
        recipientUserId,
        "New Repair Chat Message",
        notificationMessage,
        "Chat",
        $"/Chat/RepairChat?repairId={repairId}"
    );
            }


            // =========================
            // NATIVE FORM REDIRECT
            // =========================
            return RedirectToAction(
                "RepairChat",
                new
                {
                    repairId
                });
        }
    }
}