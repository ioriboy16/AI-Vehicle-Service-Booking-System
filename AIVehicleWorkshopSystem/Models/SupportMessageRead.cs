using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class SupportMessageRead
    {
        [Key]
        public int SupportMessageReadId { get; set; }

        [Required]
        public int MessageId { get; set; }

        [ForeignKey("MessageId")]
        public SupportMessage? Message { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime ReadAt { get; set; } = DateTime.Now;
    }
}