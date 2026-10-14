using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AIVehicleWorkshopSystem.ViewModels
{
    public class AIDiagnosticViewModel
    {
        [Required(ErrorMessage = "Please select a vehicle.")]
        [Display(Name = "Vehicle")]
        public int? VehicleId { get; set; }

        [Required(ErrorMessage = "Please describe the vehicle symptoms.")]
        [StringLength(
            2000,
            MinimumLength = 10,
            ErrorMessage = "Please provide at least 10 characters and no more than 2000 characters.")]
        [Display(Name = "Vehicle Symptoms")]
        public string Symptoms { get; set; } = string.Empty;

        public List<SelectListItem> Vehicles { get; set; }
            = new List<SelectListItem>();
    }
}