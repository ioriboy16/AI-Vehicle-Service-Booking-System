using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }


        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;


        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;


        [Required]
        [StringLength(50)]
        public string Type { get; set; } = "General";


        [StringLength(500)]
        public string? RelatedUrl { get; set; }


        public bool IsRead { get; set; } = false;


        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}