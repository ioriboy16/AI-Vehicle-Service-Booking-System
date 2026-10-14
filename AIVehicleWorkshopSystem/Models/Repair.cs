using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class Repair
    {
        [Key]
        public int RepairId { get; set; }

        // =========================
        // APPOINTMENT
        // =========================
        [Required]
        public int AppointmentId { get; set; }

        [ForeignKey("AppointmentId")]
        public Appointment? Appointment { get; set; }

        // =========================
        // ASSIGNED MECHANIC
        // =========================
        [Required]
        public string MechanicId { get; set; } = string.Empty;

        // =========================
        // REPAIR STATUS
        // =========================
        [Required]
        [StringLength(50)]
        public string RepairStatus { get; set; } = "Assigned";

        // =========================
        // DIAGNOSIS
        // =========================
        [StringLength(1000)]
        public string? Diagnosis { get; set; }

        // =========================
        // REPAIR NOTES
        // =========================
        [StringLength(2000)]
        public string? RepairNotes { get; set; }

        // =========================
        // DATE / TIME
        // =========================
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}