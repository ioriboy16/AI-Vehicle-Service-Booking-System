using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using AIVehicleWorkshopSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class AppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public AppointmentController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // =========================
        // MY APPOINTMENTS
        // =========================
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var appointments = await _context.Appointments
                .Include(a => a.Vehicle)
                .Where(a => a.CustomerId == userId)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToListAsync();

            return View(appointments);
        }

        // =========================
        // APPOINTMENT DETAILS
        // =========================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Vehicle)
                .FirstOrDefaultAsync(a =>
                    a.AppointmentId == id &&
                    a.CustomerId == userId);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // =========================
        // BOOK APPOINTMENT - GET
        // =========================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var vehicles = await _context.Vehicles
                .Where(v => v.UserId == userId)
                .OrderBy(v => v.PlateNumber)
                .ToListAsync();

            ViewBag.Vehicles = new SelectList(
                vehicles,
                "VehicleId",
                "PlateNumber"
            );

            ViewBag.HasVehicles = vehicles.Any();

            return View();
        }

        // =========================
        // BOOK APPOINTMENT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Appointment appointment)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Automatically link appointment
            // to the logged-in customer
            appointment.CustomerId = userId;

            // New appointments always start as Pending
            appointment.AppointmentStatus = "Pending";

            // Automatically record creation time
            appointment.CreatedAt = DateTime.Now;

            // CustomerId is not entered manually
            ModelState.Remove(nameof(Appointment.CustomerId));

            // =========================
            // CHECK VEHICLE OWNERSHIP
            // =========================
            var vehicleExists = await _context.Vehicles
                .AnyAsync(v =>
                    v.VehicleId == appointment.VehicleId &&
                    v.UserId == userId);

            if (!vehicleExists)
            {
                ModelState.AddModelError(
                    nameof(Appointment.VehicleId),
                    "Please select a valid vehicle.");
            }

            // =========================
            // CHECK DATE AND TIME
            // =========================
            var appointmentDateTime =
                appointment.AppointmentDate.Date
                + appointment.AppointmentTime;

            if (appointmentDateTime <= DateTime.Now)
            {
                ModelState.AddModelError(
                    nameof(Appointment.AppointmentDate),
                    "Appointment date and time must be in the future.");
            }

            // =========================
            // SAVE APPOINTMENT
            // =========================
            if (ModelState.IsValid)
            {
                _context.Appointments.Add(appointment);

                await _context.SaveChangesAsync();

                // Notify all active admins
                await _notificationService.CreateForRoleAsync(
                    "Admin",
                    "New Appointment Request",
                    $"A new appointment #{appointment.AppointmentId} has been submitted.",
                    "Appointment",
                    $"/Admin/AppointmentDetails/{appointment.AppointmentId}"
                );

                TempData["SuccessMessage"] =
                    "Appointment booked successfully.";

                return RedirectToAction(nameof(Index));
            }

            // Reload dropdown if validation fails
            await LoadVehicleDropdown(
                userId,
                appointment.VehicleId
            );

            return View(appointment);
        }

        // =========================
        // EDIT APPOINTMENT - GET
        // =========================
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a =>
                    a.AppointmentId == id &&
                    a.CustomerId == userId);

            if (appointment == null)
            {
                return NotFound();
            }

            // Only Pending appointments can be edited
            if (appointment.AppointmentStatus != "Pending")
            {
                TempData["ErrorMessage"] =
                    "Only pending appointments can be edited.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = appointment.AppointmentId }
                );
            }

            await LoadVehicleDropdown(
                userId,
                appointment.VehicleId
            );

            return View(appointment);
        }

        // =========================
        // EDIT APPOINTMENT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Appointment appointment)
        {
            if (id != appointment.AppointmentId)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var existingAppointment =
                await _context.Appointments
                    .FirstOrDefaultAsync(a =>
                        a.AppointmentId == id &&
                        a.CustomerId == userId);

            if (existingAppointment == null)
            {
                return NotFound();
            }

            // Prevent editing after status changes
            if (existingAppointment.AppointmentStatus != "Pending")
            {
                TempData["ErrorMessage"] =
                    "Only pending appointments can be edited.";

                return RedirectToAction(
                    nameof(Details),
                    new { id }
                );
            }

            ModelState.Remove(nameof(Appointment.CustomerId));
            ModelState.Remove(nameof(Appointment.AppointmentStatus));

            // =========================
            // CHECK VEHICLE OWNERSHIP
            // =========================
            var vehicleExists = await _context.Vehicles
                .AnyAsync(v =>
                    v.VehicleId == appointment.VehicleId &&
                    v.UserId == userId);

            if (!vehicleExists)
            {
                ModelState.AddModelError(
                    nameof(Appointment.VehicleId),
                    "Please select a valid vehicle.");
            }

            // =========================
            // CHECK DATE AND TIME
            // =========================
            var appointmentDateTime =
                appointment.AppointmentDate.Date
                + appointment.AppointmentTime;

            if (appointmentDateTime <= DateTime.Now)
            {
                ModelState.AddModelError(
                    nameof(Appointment.AppointmentDate),
                    "Appointment date and time must be in the future.");
            }

            // =========================
            // UPDATE APPOINTMENT
            // =========================
            if (ModelState.IsValid)
            {
                existingAppointment.VehicleId =
                    appointment.VehicleId;

                existingAppointment.AppointmentDate =
                    appointment.AppointmentDate;

                existingAppointment.AppointmentTime =
                    appointment.AppointmentTime;

                existingAppointment.ServiceType =
                    appointment.ServiceType;

                existingAppointment.CustomerComplaint =
                    appointment.CustomerComplaint;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Appointment updated successfully.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = existingAppointment.AppointmentId }
                );
            }

            appointment.CustomerId = userId;

            appointment.AppointmentStatus =
                existingAppointment.AppointmentStatus;

            appointment.CreatedAt =
                existingAppointment.CreatedAt;

            await LoadVehicleDropdown(
                userId,
                appointment.VehicleId
            );

            return View(appointment);
        }

        // =========================
        // CANCEL APPOINTMENT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a =>
                    a.AppointmentId == id &&
                    a.CustomerId == userId);

            if (appointment == null)
            {
                return NotFound();
            }

            // Only Pending appointments can be cancelled
            if (appointment.AppointmentStatus != "Pending")
            {
                TempData["ErrorMessage"] =
                    "Only pending appointments can be cancelled.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = appointment.AppointmentId }
                );
            }

            // Do not delete appointment.
            // Keep it as history.
            appointment.AppointmentStatus = "Cancelled";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Appointment cancelled successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id = appointment.AppointmentId }
            );
        }

        // =========================
        // VEHICLE DROPDOWN HELPER
        // =========================
        private async Task LoadVehicleDropdown(
            string userId,
            int? selectedVehicleId = null)
        {
            var vehicles = await _context.Vehicles
                .Where(v => v.UserId == userId)
                .OrderBy(v => v.PlateNumber)
                .ToListAsync();

            ViewBag.Vehicles = new SelectList(
                vehicles,
                "VehicleId",
                "PlateNumber",
                selectedVehicleId
            );

            ViewBag.HasVehicles = vehicles.Any();
        }
    }
}