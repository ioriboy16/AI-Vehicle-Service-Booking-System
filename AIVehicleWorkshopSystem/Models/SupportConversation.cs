using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class SupportConversation
    {
        [Key]
        public int ConversationId { get; set; }

        // Customer who owns this conversation
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [ForeignKey("CustomerId")]
        public ApplicationUser? Customer { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<SupportMessage> Messages { get; set; }
            = new List<SupportMessage>();
    }
}