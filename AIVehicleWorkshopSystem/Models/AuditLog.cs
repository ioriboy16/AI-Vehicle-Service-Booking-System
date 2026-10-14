using System.ComponentModel.DataAnnotations;

namespace AIVehicleWorkshopSystem.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        // User who performed the action
        public string? UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string UserRole { get; set; } = string.Empty;

        // Example:
        // APPROVE APPOINTMENT
        // COMPLETE REPAIR
        // CREATE INVOICE
        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        // Example:
        // Appointment
        // Repair
        // Invoice
        // User
        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        // ID of affected record
        [StringLength(100)]
        public string? EntityId { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}