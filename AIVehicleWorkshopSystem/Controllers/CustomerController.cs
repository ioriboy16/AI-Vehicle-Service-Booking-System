using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using AIVehicleWorkshopSystem.Services;
using AIVehicleWorkshopSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AIVehicleWorkshopSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AIDiagnosticService _aiDiagnosticService;

        public CustomerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AIDiagnosticService aiDiagnosticService)
        {
            _context = context;
            _userManager = userManager;
            _aiDiagnosticService = aiDiagnosticService;
        }

        // =========================
        // CUSTOMER DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            var customerId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized();
            }

            var customer =
                await _userManager.FindByIdAsync(customerId);

            ViewBag.CustomerUsername =
                customer?.UserName ?? "Customer";

            ViewBag.CustomerName =
                !string.IsNullOrWhiteSpace(customer?.FullName)
                    ? customer.FullName
                    : customer?.UserName ?? "Customer";


            // =========================
            // VEHICLE COUNT
            // =========================
            ViewBag.TotalVehicles =
                await _context.Vehicles
                    .CountAsync(v =>
                        v.UserId == customerId);


            // =========================
            // UPCOMING APPOINTMENTS
            // =========================
            var today = DateTime.Today;

            ViewBag.UpcomingAppointments =
                await _context.Appointments
                    .CountAsync(a =>
                        a.CustomerId == customerId &&
                        a.AppointmentDate >= today &&
                        a.AppointmentStatus != "Cancelled" &&
                        a.AppointmentStatus != "Rejected");


            // =========================
            // REPAIRS IN PROGRESS
            // =========================
            ViewBag.RepairsInProgress =
                await _context.Repairs
                    .Include(r => r.Appointment)
                    .CountAsync(r =>
                        r.Appointment != null &&
                        r.Appointment.CustomerId == customerId &&
                        r.RepairStatus == "In Progress");


            // =========================
            // PENDING INVOICES
            // =========================
            ViewBag.PendingInvoices =
                await _context.Invoices
                    .Include(i => i.Repair)
                        .ThenInclude(r => r.Appointment)
                    .CountAsync(i =>
                        i.Repair != null &&
                        i.Repair.Appointment != null &&
                        i.Repair.Appointment.CustomerId == customerId &&
                        i.PaymentStatus != "Paid");


            // =========================
            // NEXT APPOINTMENT
            // =========================
            var nextAppointment =
                await _context.Appointments
                    .Include(a => a.Vehicle)
                    .Where(a =>
                        a.CustomerId == customerId &&
                        a.AppointmentDate >= today &&
                        a.AppointmentStatus != "Cancelled" &&
                        a.AppointmentStatus != "Rejected")
                    .OrderBy(a => a.AppointmentDate)
                    .ThenBy(a => a.AppointmentTime)
                    .FirstOrDefaultAsync();

            ViewBag.NextAppointment =
                nextAppointment;


            // =========================
            // CURRENT REPAIR
            // =========================
            var currentRepair =
                await _context.Repairs
                    .Include(r => r.Appointment)
                        .ThenInclude(a => a.Vehicle)
                    .Where(r =>
                        r.Appointment != null &&
                        r.Appointment.CustomerId == customerId &&
                        r.RepairStatus != "Completed")
                    .OrderByDescending(r => r.UpdatedAt)
                    .FirstOrDefaultAsync();

            ViewBag.CurrentRepair =
                currentRepair;


            // =========================
            // LATEST REPAIR UPDATE
            // =========================
            if (currentRepair != null)
            {
                ViewBag.LatestRepairProgress =
                    await _context.RepairProgresses
                        .Where(p =>
                            p.RepairId == currentRepair.RepairId)
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();
            }
            else
            {
                ViewBag.LatestRepairProgress = null;
            }


            return View();
        }


        // =========================
        // CUSTOMER REPAIR TRACKING
        // =========================
        [HttpGet]
        public async Task<IActionResult> RepairTracking()
        {
            var userId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var repairs =
                await _context.Repairs
                    .Include(r => r.Appointment)
                    .ThenInclude(a => a!.Vehicle)
                    .Where(r =>
                        r.Appointment != null &&
                        r.Appointment.CustomerId == userId)
                    .OrderByDescending(r =>
                        r.UpdatedAt)
                    .ToListAsync();

            // =========================
            // GET MECHANIC NAMES
            // =========================
            var mechanicNames =
                new Dictionary<string, string>();

            foreach (var repair in repairs)
            {
                if (!mechanicNames.ContainsKey(
                    repair.MechanicId))
                {
                    var mechanic =
                        await _userManager.FindByIdAsync(
                            repair.MechanicId);

                    mechanicNames[repair.MechanicId] =
                        mechanic?.FullName
                        ?? mechanic?.UserName
                        ?? "Unknown Mechanic";
                }
            }

            // =========================
            // GET UNREAD CHAT COUNTS
            // =========================
            var unreadChatCounts =
                new Dictionary<int, int>();

            foreach (var repair in repairs)
            {
                var conversation =
                    await _context.ChatConversations
                        .FirstOrDefaultAsync(c =>
                            c.RepairId ==
                            repair.RepairId);

                var unreadCount = 0;

                if (conversation != null)
                {
                    unreadCount =
                        await _context.ChatMessages
                            .Where(m =>
                                m.ConversationId ==
                                    conversation.ConversationId &&

                                m.SenderId != userId &&

                                !_context.ChatMessageReads.Any(r =>
                                    r.MessageId ==
                                        m.MessageId &&
                                    r.UserId ==
                                        userId))
                            .CountAsync();
                }

                unreadChatCounts[
                    repair.RepairId] =
                    unreadCount;
            }

            ViewBag.MechanicNames =
                mechanicNames;

            ViewBag.UnreadChatCounts =
                unreadChatCounts;

            return View(repairs);
        }


        // =========================
        // CUSTOMER REPAIR DETAILS
        // =========================
        public async Task<IActionResult> RepairDetails(
            int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customerId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized();
            }

            var repair =
                await _context.Repairs
                    .Include(r => r.Appointment)
                    .ThenInclude(a => a!.Vehicle)
                    .FirstOrDefaultAsync(r =>
                        r.RepairId == id &&
                        r.Appointment != null &&
                        r.Appointment.CustomerId ==
                            customerId);

            if (repair == null)
            {
                return NotFound();
            }

            // =========================
            // REPAIR HISTORY
            // =========================
            var progressHistory =
                await _context.RepairProgresses
                    .Where(p =>
                        p.RepairId ==
                        repair.RepairId)
                    .OrderByDescending(p =>
                        p.CreatedAt)
                    .ToListAsync();

            ViewBag.ProgressHistory =
                progressHistory;

            // =========================
            // MECHANIC INFORMATION
            // =========================
            var mechanic =
                await _userManager.FindByIdAsync(
                    repair.MechanicId);

            ViewBag.MechanicUsername =
                mechanic?.UserName
                ?? "Assigned Mechanic";

            // =========================
            // UNREAD CHAT COUNT
            // =========================
            var conversation =
                await _context.ChatConversations
                    .FirstOrDefaultAsync(c =>
                        c.RepairId ==
                        repair.RepairId);

            var unreadCount = 0;

            if (conversation != null)
            {
                unreadCount =
                    await _context.ChatMessages
                        .Where(m =>
                            m.ConversationId ==
                                conversation.ConversationId &&

                            m.SenderId != customerId &&

                            !_context.ChatMessageReads.Any(r =>
                                r.MessageId ==
                                    m.MessageId &&
                                r.UserId ==
                                    customerId))
                        .CountAsync();
            }

            ViewBag.UnreadChatCount =
                unreadCount;

            return View(repair);
        }


        // =========================
        // AI DIAGNOSTIC - GET
        // =========================
        [HttpGet]
        public async Task<IActionResult> AIDiagnosis()
        {
            var userId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var model =
                new AIDiagnosticViewModel
                {
                    Vehicles =
                        await GetCustomerVehicleOptions(
                            userId)
                };

            // =========================
            // AI DIAGNOSTIC HISTORY
            // =========================
            await LoadDiagnosticHistory(userId);

            return View(model);
        }


        // =========================
        // AI DIAGNOSTIC - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AIDiagnosis(
            AIDiagnosticViewModel model)
        {
            var userId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // =========================
            // VALIDATE VEHICLE
            // =========================
            Vehicle? selectedVehicle = null;

            if (model.VehicleId.HasValue)
            {
                selectedVehicle =
                    await _context.Vehicles
                        .FirstOrDefaultAsync(v =>
                            v.VehicleId ==
                                model.VehicleId.Value &&
                            v.UserId ==
                                userId);

                if (selectedVehicle == null)
                {
                    ModelState.AddModelError(
                        nameof(model.VehicleId),
                        "The selected vehicle is invalid.");
                }
            }

            // =========================
            // VALIDATION FAILED
            // =========================
            if (!ModelState.IsValid ||
                selectedVehicle == null)
            {
                model.Vehicles =
                    await GetCustomerVehicleOptions(
                        userId);

                // Reload history so it still appears
                // when validation fails.
                await LoadDiagnosticHistory(userId);

                return View(model);
            }

            try
            {
                // =========================
                // GET VEHICLE SERVICE HISTORY
                // =========================
                var previousRepairs =
                    await _context.Repairs
                        .Include(r => r.Appointment)
                        .Where(r =>
                            r.Appointment != null &&
                            r.Appointment.VehicleId ==
                                selectedVehicle.VehicleId)
                        .OrderByDescending(r =>
                            r.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                // =========================
                // BUILD SERVICE HISTORY
                // =========================
                var historyBuilder =
                    new StringBuilder();

                if (previousRepairs.Count == 0)
                {
                    historyBuilder.AppendLine(
                        "No previous repair history is available.");
                }
                else
                {
                    foreach (var repair in previousRepairs)
                    {
                        historyBuilder.AppendLine(
                            $"Service Date: " +
                            $"{repair.CreatedAt:dd MMM yyyy}");

                        historyBuilder.AppendLine(
                            $"Service Type: " +
                            $"{repair.Appointment?.ServiceType ?? "Not available"}");

                        historyBuilder.AppendLine(
                            $"Customer Complaint: " +
                            $"{repair.Appointment?.CustomerComplaint ?? "Not available"}");

                        historyBuilder.AppendLine(
                            $"Issue Found: " +
                            $"{repair.Diagnosis ?? "Not recorded"}");

                        historyBuilder.AppendLine(
                            $"Action Taken: " +
                            $"{repair.RepairNotes ?? "Not recorded"}");

                        historyBuilder.AppendLine(
                            $"Repair Status: " +
                            $"{repair.RepairStatus}");

                        historyBuilder.AppendLine();
                    }
                }

                // =========================
                // CALL ACTUAL AI SERVICE
                // =========================
                var aiResult =
                    await _aiDiagnosticService
                        .AnalyseVehicleAsync(
                            selectedVehicle,
                            model.Symptoms,
                            historyBuilder.ToString());

                // =========================
                // SAVE AI DIAGNOSTIC
                // =========================
                var diagnostic =
                    new AIDiagnostic
                    {
                        CustomerId =
                            userId,

                        VehicleId =
                            selectedVehicle.VehicleId,

                        Symptoms =
                            model.Symptoms.Trim(),

                        PossibleIssue =
                            aiResult.PossibleIssue,

                        RecommendedInspection =
                            aiResult.RecommendedInspection,

                        MaintenanceSuggestion =
                            aiResult.MaintenanceSuggestion,

                        Priority =
                            aiResult.Priority,

                        Status =
                            "Completed",

                        ReviewStatus =
                            "Pending Review",

                        CreatedAt =
                            DateTime.Now,

                        CompletedAt =
                            DateTime.Now
                    };

                _context.AIDiagnostics.Add(
                    diagnostic);

                await _context.SaveChangesAsync();

                // =========================
                // REDIRECT TO RESULT
                // =========================
                return RedirectToAction(
                    nameof(AIDiagnosisResult),
                    new
                    {
                        id =
                            diagnostic.DiagnosticId
                    });
            }
            catch (Exception ex)
            {
                // =========================
                // LOG ACTUAL ERROR
                // =========================
                Console.WriteLine();
                Console.WriteLine(
                    "==========================================");

                Console.WriteLine(
                    "AI DIAGNOSTIC ERROR");

                Console.WriteLine(
                    "==========================================");

                Console.WriteLine(
                    ex.ToString());

                Console.WriteLine(
                    "==========================================");

                Console.WriteLine();

                // =========================
                // SAFE CUSTOMER MESSAGE
                // =========================
                ModelState.AddModelError(
                    string.Empty,
                    "The AI diagnostic service is currently unable " +
                    "to analyse the vehicle symptoms. " +
                    "Please try again.");

                model.Vehicles =
                    await GetCustomerVehicleOptions(
                        userId);

                // Reload history even if AI API fails.
                await LoadDiagnosticHistory(userId);

                return View(model);
            }
        }


        // =========================
        // AI DIAGNOSTIC RESULT
        // =========================
        [HttpGet]
        public async Task<IActionResult>
            AIDiagnosisResult(int id)
        {
            var userId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Customer can only access
            // their own AI diagnostic result.
            var diagnostic =
                await _context.AIDiagnostics
                    .Include(d => d.Vehicle)
                    .FirstOrDefaultAsync(d =>
                        d.DiagnosticId == id &&
                        d.CustomerId == userId);

            if (diagnostic == null)
            {
                return NotFound();
            }

            return View(diagnostic);
        }


        // =========================
        // LOAD AI DIAGNOSTIC HISTORY
        // =========================
        private async Task LoadDiagnosticHistory(
            string userId)
        {
            var diagnosticHistory =
                await _context.AIDiagnostics
                    .Include(d => d.Vehicle)
                    .Where(d =>
                        d.CustomerId == userId)
                    .OrderByDescending(d =>
                        d.CreatedAt)
                    .ToListAsync();

            ViewBag.DiagnosticHistory =
                diagnosticHistory;
        }


        // =========================
        // CUSTOMER VEHICLE OPTIONS
        // =========================
        private async Task<List<SelectListItem>>
            GetCustomerVehicleOptions(
                string userId)
        {
            return await _context.Vehicles
                .Where(v =>
                    v.UserId == userId)
                .OrderBy(v =>
                    v.PlateNumber)
                .Select(v =>
                    new SelectListItem
                    {
                        Value =
                            v.VehicleId.ToString(),

                        Text =
                            v.Brand + " " +
                            v.Model + " - " +
                            v.PlateNumber
                    })
                .ToListAsync();
        }
        // =====================================================
        // CUSTOMER INVOICES
        // =====================================================

        // =========================
        // MY INVOICES
        // =========================
        [HttpGet]
        public async Task<IActionResult> Invoices()
        {
            var customerId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized();
            }

            var invoices =
                await _context.Invoices
                    .Include(i => i.InvoiceItems)
                    .Include(i => i.Repair)
                        .ThenInclude(r => r!.Appointment)
                        .ThenInclude(a => a!.Vehicle)
                    .Where(i =>
                        i.Repair != null &&
                        i.Repair.Appointment != null &&
                        i.Repair.Appointment.CustomerId ==
                            customerId)
                    .OrderByDescending(i =>
                        i.InvoiceDate)
                    .ThenByDescending(i =>
                        i.InvoiceId)
                    .ToListAsync();

            return View(invoices);
        }


        // =========================
        // CUSTOMER INVOICE DETAILS
        // =========================
        [HttpGet]
        public async Task<IActionResult> InvoiceDetails(
            int id)
        {
            var customerId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized();
            }

            var invoice =
                await _context.Invoices
                    .Include(i => i.InvoiceItems)
                    .Include(i => i.Repair)
                        .ThenInclude(r => r!.Appointment)
                        .ThenInclude(a => a!.Vehicle)
                    .FirstOrDefaultAsync(i =>
                        i.InvoiceId == id &&
                        i.Repair != null &&
                        i.Repair.Appointment != null &&
                        i.Repair.Appointment.CustomerId ==
                            customerId);

            if (invoice == null)
            {
                return NotFound();
            }

            var customer =
                await _userManager.FindByIdAsync(
                    customerId);

            ViewBag.CustomerName =
                customer?.FullName
                ?? customer?.UserName
                ?? customer?.Email
                ?? "Customer";

            ViewBag.CustomerEmail =
                customer?.Email ?? "-";

            return View(invoice);
        }
    }
}