using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class ChatMessageRead
    {
        [Key]
        public int ChatMessageReadId { get; set; }

        [Required]
        public int MessageId { get; set; }

        [ForeignKey("MessageId")]
        public ChatMessage? ChatMessage { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime ReadAt { get; set; } = DateTime.Now;
    }
}