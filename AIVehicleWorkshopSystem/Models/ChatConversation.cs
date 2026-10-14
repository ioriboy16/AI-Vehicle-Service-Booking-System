using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class ChatConversation
    {
        [Key]
        public int ConversationId { get; set; }

        [Required]
        public int RepairId { get; set; }

        [ForeignKey("RepairId")]
        public Repair? Repair { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<ChatMessage> Messages { get; set; }
            = new List<ChatMessage>();
    }
}