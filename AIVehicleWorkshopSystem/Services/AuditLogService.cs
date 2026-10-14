using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace AIVehicleWorkshopSystem.Services
{
    public class AuditLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditLogService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        public async Task LogAsync(
            ClaimsPrincipal principal,
            string action,
            string entityType,
            string? entityId,
            string? description)
        {
            var user =
                await _userManager.GetUserAsync(principal);

            if (user == null)
            {
                return;
            }


            // Get current user's role
            var roles =
                await _userManager.GetRolesAsync(user);

            var role =
                roles.FirstOrDefault()
                ?? "Unknown";


            var auditLog =
                new AuditLog
                {
                    UserId = user.Id,

                    UserName =
                        !string.IsNullOrWhiteSpace(user.FullName)
                            ? user.FullName
                            : user.UserName
                                ?? user.Email
                                ?? "Unknown User",

                    UserRole = role,

                    Action = action,

                    EntityType = entityType,

                    EntityId = entityId,

                    Description = description,

                    CreatedAt = DateTime.Now
                };


            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();
        }
    }
}