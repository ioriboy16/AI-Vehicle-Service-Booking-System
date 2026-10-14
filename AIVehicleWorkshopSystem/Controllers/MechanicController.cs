using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using AIVehicleWorkshopSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Controllers
{
    [Authorize(Roles = "Mechanic")]
    public class MechanicController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public MechanicController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }


        // =========================
        // DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            var mechanicId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(mechanicId))
            {
                return Unauthorized();
            }

            var assignedRepairs = await _context.Repairs
                .CountAsync(r =>
                    r.MechanicId == mechanicId &&
                    r.RepairStatus == "Assigned");

            var repairsInProgress = await _context.Repairs
                .CountAsync(r =>
                    r.MechanicId == mechanicId &&
                    r.RepairStatus == "In Progress");

            var completedRepairs = await _context.Repairs
                .CountAsync(r =>
                    r.MechanicId == mechanicId &&
                    r.RepairStatus == "Completed");

            ViewBag.AssignedRepairs = assignedRepairs;
            ViewBag.RepairsInProgress = repairsInProgress;
            ViewBag.CompletedRepairs = completedRepairs;

            return View();
        }


        // =========================
        // MY REPAIRS
        // =========================
        [HttpGet]
        public async Task<IActionResult> Repairs()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var repairs = await _context.Repairs
                .Include(r => r.Appointment)
                    .ThenInclude(a => a!.Vehicle)
                .Where(r => r.MechanicId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // =========================
            // UNREAD CHAT COUNTS
            // =========================
            var unreadChatCounts =
                new Dictionary<int, int>();

            foreach (var repair in repairs)
            {
                var conversation =
                    await _context.ChatConversations
                        .FirstOrDefaultAsync(c =>
                            c.RepairId == repair.RepairId);

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
                                    r.MessageId == m.MessageId &&
                                    r.UserId == userId))
                            .CountAsync();
                }

                unreadChatCounts[repair.RepairId] =
                    unreadCount;
            }

            ViewBag.UnreadChatCounts =
                unreadChatCounts;

            return View(repairs);
        }


        // =========================
        // REPAIR DETAILS
        // =========================
        public async Task<IActionResult> RepairDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var mechanicId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(mechanicId))
            {
                return Unauthorized();
            }

            var repair = await _context.Repairs
                .Include(r => r.Appointment)
                    .ThenInclude(a => a!.Vehicle)
                .FirstOrDefaultAsync(r =>
                    r.RepairId == id &&
                    r.MechanicId == mechanicId);

            if (repair == null)
            {
                return NotFound();
            }

            var progressHistory = await _context.RepairProgresses
                .Where(p => p.RepairId == repair.RepairId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.ProgressHistory = progressHistory;

            if (repair.Appointment != null)
            {
                var customer = await _userManager.FindByIdAsync(
                    repair.Appointment.CustomerId
                );

                ViewBag.CustomerUsername =
                    customer?.UserName ?? "Unknown Customer";

                ViewBag.CustomerEmail =
                    customer?.Email ?? "Unknown Email";
            }
            else
            {
                ViewBag.CustomerUsername = "Unknown Customer";
                ViewBag.CustomerEmail = "Unknown Email";
            }

            return View(repair);
        }


        // =========================
        // START REPAIR
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartRepair(int id)
        {
            var mechanicId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(mechanicId))
            {
                return Unauthorized();
            }

            var repair = await _context.Repairs
                .FirstOrDefaultAsync(r =>
                    r.RepairId == id &&
                    r.MechanicId == mechanicId);

            if (repair == null)
            {
                return NotFound();
            }

            if (repair.RepairStatus != "Assigned")
            {
                TempData["ErrorMessage"] =
                    "Only assigned repairs can be started.";

                return RedirectToAction(
                    nameof(RepairDetails),
                    new { id }
                );
            }

            var now = DateTime.Now;

            repair.RepairStatus = "In Progress";
            repair.StartedAt = now;
            repair.UpdatedAt = now;

            var progress = new RepairProgress
            {
                RepairId = repair.RepairId,
                Status = "In Progress",
                Description = "Repair work has started.",
                UpdatedBy = mechanicId,
                CreatedAt = now
            };

            _context.RepairProgresses.Add(progress);

            await _context.SaveChangesAsync();

            // Get the customer who owns this repair
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a =>
                    a.AppointmentId == repair.AppointmentId);

            if (appointment != null)
            {
                await _notificationService.CreateAsync(
                    appointment.CustomerId,
                    "Repair Started",
                    $"Repair #{repair.RepairId} has been started.",
                    "Repair",
                    $"/Customer/RepairDetails/{repair.RepairId}"
                );
            }

            TempData["SuccessMessage"] =
                "Repair started successfully.";

            return RedirectToAction(
                nameof(RepairDetails),
                new { id }
            );
        }


        // =========================
        // SAVE WORKSHOP UPDATE
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveWorkshopUpdate(
            int id,
            string? issueFound,
            string? actionTaken)
        {
            var mechanicId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(mechanicId))
            {
                return Unauthorized();
            }

            var repair = await _context.Repairs
                .FirstOrDefaultAsync(r =>
                    r.RepairId == id &&
                    r.MechanicId == mechanicId);

            if (repair == null)
            {
                return NotFound();
            }

            if (repair.RepairStatus != "In Progress")
            {
                TempData["ErrorMessage"] =
                    "Only repairs in progress can be updated.";

                return RedirectToAction(
                    nameof(RepairDetails),
                    new { id }
                );
            }

            if (string.IsNullOrWhiteSpace(issueFound) &&
                string.IsNullOrWhiteSpace(actionTaken))
            {
                TempData["ErrorMessage"] =
                    "Please enter an issue found or action taken.";

                return RedirectToAction(
                    nameof(RepairDetails),
                    new { id }
                );
            }

            repair.Diagnosis = issueFound?.Trim();
            repair.RepairNotes = actionTaken?.Trim();
            repair.UpdatedAt = DateTime.Now;

            var descriptionParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(issueFound))
            {
                descriptionParts.Add(
                    $"Issue Found: {issueFound.Trim()}"
                );
            }

            if (!string.IsNullOrWhiteSpace(actionTaken))
            {
                descriptionParts.Add(
                    $"Action Taken: {actionTaken.Trim()}"
                );
            }

            var progress = new RepairProgress
            {
                RepairId = repair.RepairId,
                Status = "Update",
                Description = string.Join(
                    " | ",
                    descriptionParts
                ),
                UpdatedBy = mechanicId,
                CreatedAt = DateTime.Now
            };

            _context.RepairProgresses.Add(progress);

            await _context.SaveChangesAsync();

            // Get customer for this repair
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a =>
                    a.AppointmentId == repair.AppointmentId);

            if (appointment != null)
            {
                await _notificationService.CreateAsync(
                    appointment.CustomerId,
                    "Repair Updated",
                    $"Repair #{repair.RepairId} has received a new progress update.",
                    "Repair",
                    $"/Customer/RepairDetails/{repair.RepairId}"
                );
            }

            TempData["SuccessMessage"] =
                "Workshop update saved successfully.";

            return RedirectToAction(
                nameof(RepairDetails),
                new { id }
            );
        }


        // =========================
        // COMPLETE REPAIR
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteRepair(int id)
        {
            var mechanicId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(mechanicId))
            {
                return Unauthorized();
            }

            var repair = await _context.Repairs
                .FirstOrDefaultAsync(r =>
                    r.RepairId == id &&
                    r.MechanicId == mechanicId);

            if (repair == null)
            {
                return NotFound();
            }

            if (repair.RepairStatus != "In Progress")
            {
                TempData["ErrorMessage"] =
                    "Only repairs in progress can be completed.";

                return RedirectToAction(
                    nameof(RepairDetails),
                    new { id }
                );
            }

            if (string.IsNullOrWhiteSpace(repair.Diagnosis))
            {
                TempData["ErrorMessage"] =
                    "Please record the issue found before completing the repair.";

                return RedirectToAction(
                    nameof(RepairDetails),
                    new { id }
                );
            }

            if (string.IsNullOrWhiteSpace(repair.RepairNotes))
            {
                TempData["ErrorMessage"] =
                    "Please record the action taken before completing the repair.";

                return RedirectToAction(
                    nameof(RepairDetails),
                    new { id }
                );
            }

            var now = DateTime.Now;

            repair.RepairStatus = "Completed";
            repair.CompletedAt = now;
            repair.UpdatedAt = now;

            var progress = new RepairProgress
            {
                RepairId = repair.RepairId,
                Status = "Completed",
                Description = "Repair work has been completed.",
                UpdatedBy = mechanicId,
                CreatedAt = now
            };

            _context.RepairProgresses.Add(progress);

            await _context.SaveChangesAsync();

            // Get customer for this repair
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a =>
                    a.AppointmentId == repair.AppointmentId);

            if (appointment != null)
            {
                await _notificationService.CreateAsync(
                    appointment.CustomerId,
                    "Repair Completed",
                    $"Repair #{repair.RepairId} has been completed.",
                    "Repair",
                    $"/Customer/RepairDetails/{repair.RepairId}"
                );
            }

            TempData["SuccessMessage"] =
                "Repair marked as completed.";

            return RedirectToAction(
                nameof(RepairDetails),
                new { id }
            );
        }


        // =====================================================
        // AI DIAGNOSTIC REVIEW
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> AIDiagnosticReview()
        {
            var mechanicId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(mechanicId))
            {
                return Unauthorized();
            }

            // Only diagnostics specifically assigned
            // by the admin to this mechanic.
            var diagnostics =
                await _context.AIDiagnostics
                    .Include(d => d.Vehicle)
                    .Where(d =>
                        d.AssignedMechanicId == mechanicId)
                    .OrderBy(d =>
                        d.ReviewStatus == "Pending Review"
                            ? 0
                            : 1)
                    .ThenByDescending(d => d.CreatedAt)
                    .ToListAsync();

            return View(diagnostics);
        }


        // =========================
        // AI DIAGNOSTIC DETAILS
        // =========================
        [HttpGet]
        public async Task<IActionResult> AIDiagnosticDetails(int id)
        {
            var mechanicId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(mechanicId))
            {
                return Unauthorized();
            }

            // Security / ownership check:
            // the diagnostic must be assigned
            // specifically to this mechanic.
            var diagnostic =
                await _context.AIDiagnostics
                    .Include(d => d.Vehicle)
                    .FirstOrDefaultAsync(d =>
                        d.DiagnosticId == id &&
                        d.AssignedMechanicId == mechanicId);

            if (diagnostic == null)
            {
                return NotFound();
            }

            return View(diagnostic);
        }


        // =========================
        // SUBMIT AI DIAGNOSTIC REVIEW
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAIDiagnosticReview(
            int id,
            string reviewDecision,
            string? mechanicComments)
        {
            var mechanicId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(mechanicId))
            {
                return Unauthorized();
            }

            // Security / ownership check:
            // only the assigned mechanic can
            // submit the diagnostic review.
            var diagnostic =
                await _context.AIDiagnostics
                    .Include(d => d.Vehicle)
                    .FirstOrDefaultAsync(d =>
                        d.DiagnosticId == id &&
                        d.AssignedMechanicId == mechanicId);

            if (diagnostic == null)
            {
                return NotFound();
            }

            // Only diagnostics waiting for review
            // can be submitted.
            if (diagnostic.ReviewStatus != "Pending Review")
            {
                TempData["ErrorMessage"] =
                    "This AI diagnostic recommendation is not available for review.";

                return RedirectToAction(
                    nameof(AIDiagnosticDetails),
                    new { id }
                );
            }

            // Validate decision
            if (reviewDecision != "Confirmed" &&
                reviewDecision != "Modified")
            {
                TempData["ErrorMessage"] =
                    "Please select a valid review decision.";

                return RedirectToAction(
                    nameof(AIDiagnosticDetails),
                    new { id }
                );
            }

            // Comments are required if the mechanic
            // modifies the AI recommendation.
            if (reviewDecision == "Modified" &&
                string.IsNullOrWhiteSpace(mechanicComments))
            {
                TempData["ErrorMessage"] =
                    "Please provide mechanic comments when modifying the AI recommendation.";

                return RedirectToAction(
                    nameof(AIDiagnosticDetails),
                    new { id }
                );
            }

            diagnostic.ReviewStatus =
                reviewDecision;

            diagnostic.ReviewedBy =
                mechanicId;

            diagnostic.MechanicComments =
                string.IsNullOrWhiteSpace(mechanicComments)
                    ? null
                    : mechanicComments.Trim();

            diagnostic.ReviewedAt =
                DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify customer that the AI diagnostic
            // has been reviewed by the mechanic
            if (diagnostic.Vehicle != null)
            {
                await _notificationService.CreateAsync(
                    diagnostic.Vehicle.UserId,
                    "AI Diagnostic Reviewed",
                    $"Your AI diagnostic #{diagnostic.DiagnosticId} has been reviewed by a mechanic.",
                    "AIDiagnostic",
                   $"/Customer/AIDiagnosisResult/{diagnostic.DiagnosticId}"
                );
            }

            TempData["SuccessMessage"] =
                reviewDecision == "Confirmed"
                    ? "AI diagnostic recommendation confirmed successfully."
                    : "AI diagnostic recommendation modified successfully.";

            return RedirectToAction(
                nameof(AIDiagnosticReview)
            );
        }
    }
}