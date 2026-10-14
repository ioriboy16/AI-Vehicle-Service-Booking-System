using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class Appointment
    {
        [Key]
        public int AppointmentId { get; set; }

        // Customer who created the appointment
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        // Customer's registered vehicle
        [Required]
        public int VehicleId { get; set; }

        [Required]
        [Display(Name = "Appointment Date")]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        [Required]
        [Display(Name = "Appointment Time")]
        [DataType(DataType.Time)]
        public TimeSpan AppointmentTime { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Service Type")]
        public string ServiceType { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        [Display(Name = "Customer Complaint")]
        public string CustomerComplaint { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string AppointmentStatus { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relationship with Vehicle table
        [ForeignKey("VehicleId")]
        public Vehicle? Vehicle { get; set; }
    }
}