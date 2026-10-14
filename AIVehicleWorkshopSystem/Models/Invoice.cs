using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public int RepairId { get; set; }

        [ForeignKey("RepairId")]
        public Repair? Repair { get; set; }

        [Required]
        [Display(Name = "Invoice Date")]
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [Display(Name = "Vehicle Mileage")]
        public int? Mileage { get; set; }

        [StringLength(2000)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Subtotal")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Paid Amount")]
        public decimal PaidAmount { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Balance")]
        public decimal BalanceAmount { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Payment Status")]
        public string PaymentStatus { get; set; } = "Unpaid";

        [Required]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<InvoiceItem> InvoiceItems { get; set; }
            = new List<InvoiceItem>();
    }
}