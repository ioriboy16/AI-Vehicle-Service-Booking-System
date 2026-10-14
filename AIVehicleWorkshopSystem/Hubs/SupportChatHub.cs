using AIVehicleWorkshopSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AIVehicleWorkshopSystem.Models;

namespace AIVehicleWorkshopSystem.Hubs
{
    [Authorize]
    public class SupportChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SupportChatHub(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task JoinSupportChat(int conversationId)
        {
            var currentUser = await _userManager.GetUserAsync(Context.User);

            if (currentUser == null)
            {
                throw new HubException("Unauthorized.");
            }

            var conversation = await _context.SupportConversations
                .FirstOrDefaultAsync(c =>
                    c.ConversationId == conversationId);

            if (conversation == null)
            {
                throw new HubException("Conversation not found.");
            }

            var isAdmin =
                await _userManager.IsInRoleAsync(currentUser, "Admin");

            var isOwner =
                conversation.CustomerId == currentUser.Id;

            // Only the customer who owns the conversation
            // or an administrator can enter.
            if (!isAdmin && !isOwner)
            {
                throw new HubException(
                    "You are not authorized to access this conversation.");
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"Support_{conversationId}");
        }
    }
}