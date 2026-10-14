using System.ComponentModel.DataAnnotations;

namespace AIVehicleWorkshopSystem.Models
{
    public class PushSubscription
    {
        [Key]
        public int PushSubscriptionId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        public string P256dh { get; set; } = string.Empty;

        [Required]
        public string Auth { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser? User { get; set; }
    }
}