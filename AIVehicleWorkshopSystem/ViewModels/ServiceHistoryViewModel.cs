using AIVehicleWorkshopSystem.Models;

namespace AIVehicleWorkshopSystem.ViewModels
{
    public class ServiceHistoryViewModel
    {
        public string SearchPlateNumber { get; set; } = string.Empty;

        public Vehicle? Vehicle { get; set; }

        public ApplicationUser? Customer { get; set; }

        public List<ServiceHistoryItemViewModel> ServiceHistory { get; set; }
            = new List<ServiceHistoryItemViewModel>();

        public bool SearchPerformed { get; set; }
    }

    public class ServiceHistoryItemViewModel
    {
        public int AppointmentId { get; set; }

        public int? RepairId { get; set; }

        public DateTime AppointmentDate { get; set; }

        public TimeSpan AppointmentTime { get; set; }

        public string ServiceType { get; set; } = string.Empty;

        public string CustomerComplaint { get; set; } = string.Empty;

        public string AppointmentStatus { get; set; } = string.Empty;

        public string RepairStatus { get; set; } = "-";

        public string IssueFound { get; set; } = "-";

        public string ActionTaken { get; set; } = "-";

        public string MechanicName { get; set; } = "-";

        public DateTime? CompletedAt { get; set; }
    }
}