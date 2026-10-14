using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using AIVehicleWorkshopSystem.Models;

namespace AIVehicleWorkshopSystem.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationHub(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }


        public override async Task OnConnectedAsync()
        {
            var user = await _userManager.GetUserAsync(Context.User);

            if (user != null)
            {
                // Each user has their own private notification group
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"NotificationUser_{user.Id}");
            }

            await base.OnConnectedAsync();
        }


        public override async Task OnDisconnectedAsync(
            Exception? exception)
        {
            var user = await _userManager.GetUserAsync(Context.User);

            if (user != null)
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    $"NotificationUser_{user.Id}");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}