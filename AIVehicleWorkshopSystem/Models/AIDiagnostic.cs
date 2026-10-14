using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIVehicleWorkshopSystem.Models
{
    public class AIDiagnostic
    {
        [Key]
        public int DiagnosticId { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        public int VehicleId { get; set; }

        [ForeignKey("VehicleId")]
        public Vehicle? Vehicle { get; set; }

        [Required]
        [StringLength(2000)]
        public string Symptoms { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? PossibleIssue { get; set; }

        [StringLength(2000)]
        public string? RecommendedInspection { get; set; }

        [StringLength(2000)]
        public string? MaintenanceSuggestion { get; set; }

        [StringLength(50)]
        public string? Priority { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Completed";


        // =========================
        // MECHANIC ASSIGNMENT
        // =========================

        // Null means the AI diagnostic
        // has not been assigned yet.
        public string? AssignedMechanicId { get; set; }


        // =========================
        // MECHANIC REVIEW
        // =========================

        [StringLength(30)]
        public string ReviewStatus { get; set; } =
            "Pending Assignment";

        public string? ReviewedBy { get; set; }

        [StringLength(2000)]
        public string? MechanicComments { get; set; }

        public DateTime? ReviewedAt { get; set; }


        // =========================
        // TIMESTAMPS
        // =========================

        public DateTime CreatedAt { get; set; } =
            DateTime.Now;

        public DateTime? CompletedAt { get; set; }
    }
}