using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatHub(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // JOIN REPAIR CHAT
        // =========================
        public async Task JoinRepairChat(int repairId)
        {
            var userId = _userManager.GetUserId(Context.User);

            if (string.IsNullOrEmpty(userId))
            {
                throw new HubException("Unauthorized.");
            }

            var repair = await _context.Repairs
                .Include(r => r.Appointment)
                .FirstOrDefaultAsync(r =>
                    r.RepairId == repairId);

            if (repair == null)
            {
                throw new HubException("Repair not found.");
            }

            // =========================
            // ACCESS CONTROL
            // =========================
            var isAdmin =
                Context.User?.IsInRole("Admin") == true;

            var isAssignedMechanic =
                Context.User?.IsInRole("Mechanic") == true &&
                repair.MechanicId == userId;

            var isRepairCustomer =
                Context.User?.IsInRole("Customer") == true &&
                repair.Appointment != null &&
                repair.Appointment.CustomerId == userId;

            if (!isAdmin &&
                !isAssignedMechanic &&
                !isRepairCustomer)
            {
                throw new HubException(
                    "You do not have permission to access this repair chat.");
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"Repair-{repairId}");
        }

        // =========================
        // LEAVE REPAIR CHAT
        // =========================
        public async Task LeaveRepairChat(int repairId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"Repair-{repairId}");
        }
    }
}