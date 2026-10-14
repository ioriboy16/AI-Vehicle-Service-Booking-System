using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class VehicleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public VehicleController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // MY VEHICLES
        // =========================
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var vehicles = await _context.Vehicles
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            return View(vehicles);
        }

        // =========================
        // REGISTER VEHICLE - GET
        // =========================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // =========================
        // REGISTER VEHICLE - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Vehicle vehicle)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // UserId is taken automatically from logged-in account
            vehicle.UserId = userId;

            // UserId is not entered through the form,
            // so remove its automatic validation error.
            ModelState.Remove(nameof(Vehicle.UserId));

            // Clean plate number
            vehicle.PlateNumber =
                vehicle.PlateNumber?.Trim().ToUpper() ?? string.Empty;

            // Check whether plate number already exists
            var plateExists = await _context.Vehicles
                .AnyAsync(v => v.PlateNumber == vehicle.PlateNumber);

            if (plateExists)
            {
                ModelState.AddModelError(
                    nameof(Vehicle.PlateNumber),
                    "This plate number is already registered.");
            }

            if (ModelState.IsValid)
            {
                vehicle.CreatedAt = DateTime.Now;

                _context.Vehicles.Add(vehicle);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Vehicle registered successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(vehicle);
        }

        // =========================
        // VEHICLE DETAILS
        // =========================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    v.UserId == userId);

            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        // =========================
        // EDIT VEHICLE - GET
        // =========================
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    v.UserId == userId);

            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        // =========================
        // EDIT VEHICLE - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Vehicle vehicle)
        {
            if (id != vehicle.VehicleId)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            ModelState.Remove(nameof(Vehicle.UserId));

            vehicle.PlateNumber =
                vehicle.PlateNumber?.Trim().ToUpper() ?? string.Empty;

            var existingVehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    v.UserId == userId);

            if (existingVehicle == null)
            {
                return NotFound();
            }

            var duplicatePlate = await _context.Vehicles
                .AnyAsync(v =>
                    v.PlateNumber == vehicle.PlateNumber &&
                    v.VehicleId != id);

            if (duplicatePlate)
            {
                ModelState.AddModelError(
                    nameof(Vehicle.PlateNumber),
                    "This plate number is already registered.");
            }

            if (ModelState.IsValid)
            {
                existingVehicle.PlateNumber =
                    vehicle.PlateNumber;

                existingVehicle.Brand =
                    vehicle.Brand;

                existingVehicle.Model =
                    vehicle.Model;

                existingVehicle.ManufacturingYear =
                    vehicle.ManufacturingYear;

                existingVehicle.Colour =
                    vehicle.Colour;

                existingVehicle.CurrentMileage =
                    vehicle.CurrentMileage;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "Vehicle updated successfully.";

                return RedirectToAction(nameof(Index));
            }

            vehicle.UserId = userId;
            vehicle.CreatedAt = existingVehicle.CreatedAt;

            return View(vehicle);
        }

        // =========================
        // DELETE VEHICLE
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id &&
                    v.UserId == userId);

            if (vehicle == null)
            {
                return NotFound();
            }

            _context.Vehicles.Remove(vehicle);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Vehicle deleted successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}