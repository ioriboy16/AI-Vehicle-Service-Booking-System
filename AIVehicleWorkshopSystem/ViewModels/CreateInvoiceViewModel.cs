using System.ComponentModel.DataAnnotations;

namespace AIVehicleWorkshopSystem.ViewModels
{
    public class CreateInvoiceViewModel
    {
        [Required]
        public int RepairId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string VehicleName { get; set; } = string.Empty;

        public string PlateNumber { get; set; } = string.Empty;

        [Display(Name = "Vehicle Mileage")]
        public int? Mileage { get; set; }

        public string? IssueFound { get; set; }

        public string? ActionTaken { get; set; }

        [StringLength(2000)]
        public string? Remarks { get; set; }

        public List<CreateInvoiceItemViewModel> Items { get; set; }
            = new List<CreateInvoiceItemViewModel>();
    }


    public class CreateInvoiceItemViewModel
    {
        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Item Type")]
        public string ItemType { get; set; } = string.Empty;

        [Required]
        [Range(0.01, 999999)]
        public decimal Quantity { get; set; } = 1;

        [Required]
        [Range(0, 999999)]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }
    }
}