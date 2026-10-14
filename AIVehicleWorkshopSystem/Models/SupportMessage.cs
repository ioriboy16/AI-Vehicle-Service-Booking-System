using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class SupportMessage
    {
        [Key]
        public int MessageId { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [ForeignKey("ConversationId")]
        public SupportConversation? Conversation { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;


        // =========================
        // TEXT MESSAGE
        // =========================

        [StringLength(2000)]
        public string? Message { get; set; }


        // =========================
        // ATTACHMENT
        // =========================

        [StringLength(255)]
        public string? AttachmentFileName { get; set; }

        [StringLength(500)]
        public string? AttachmentPath { get; set; }

        [StringLength(100)]
        public string? AttachmentContentType { get; set; }

        public long? AttachmentFileSize { get; set; }


        // =========================
        // MESSAGE INFORMATION
        // =========================

        public DateTime SentAt { get; set; } = DateTime.Now;

        public ICollection<SupportMessageRead> ReadBy { get; set; }
            = new List<SupportMessageRead>();
    }
}