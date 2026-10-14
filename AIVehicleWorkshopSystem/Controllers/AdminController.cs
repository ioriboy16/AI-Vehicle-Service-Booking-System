using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using AIVehicleWorkshopSystem.ViewModels;
using AIVehicleWorkshopSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DataEncryptionService _encryptionService;
        private readonly AuditLogService _auditLogService;
        private readonly NotificationService _notificationService;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            DataEncryptionService encryptionService,
            AuditLogService auditLogService,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _encryptionService = encryptionService;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
        }


        // =========================
        // ADMIN DASHBOARD
        // =========================
        // =========================
        // ADMIN DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            // =========================================
            // APPOINTMENT STATISTICS
            // =========================================
            var totalAppointments =
                await _context.Appointments.CountAsync();

            var pendingAppointments =
                await _context.Appointments
                    .CountAsync(a =>
                        a.AppointmentStatus == "Pending");

            var approvedAppointments =
                await _context.Appointments
                    .CountAsync(a =>
                        a.AppointmentStatus == "Approved");


            // =========================================
            // REPAIR STATISTICS
            // =========================================
            var totalRepairs =
                await _context.Repairs.CountAsync();

            var activeRepairs =
                await _context.Repairs
                    .CountAsync(r =>
                        r.RepairStatus == "Assigned" ||
                        r.RepairStatus == "In Progress");

            var completedRepairs =
                await _context.Repairs
                    .CountAsync(r =>
                        r.RepairStatus == "Completed");


            // =========================================
            // AI DIAGNOSTIC STATISTICS
            // =========================================
            var pendingAIDiagnostics =
                await _context.AIDiagnostics
                    .CountAsync(d =>
                        d.ReviewStatus == "Pending Assignment" ||
                        d.ReviewStatus == "Pending Review");


            // =========================================
            // INVOICE STATISTICS
            // =========================================
            var totalInvoices =
                await _context.Invoices.CountAsync();

            var unpaidInvoices =
                await _context.Invoices
                    .CountAsync(i =>
                        i.PaymentStatus == "Unpaid" ||
                        i.PaymentStatus == "Partially Paid");

            var outstandingBalance =
                await _context.Invoices
                    .SumAsync(i =>
                        (decimal?)i.BalanceAmount)
                ?? 0;


            // =========================================
            // USER STATISTICS
            // =========================================
            var totalUsers =
                await _userManager.Users.CountAsync();

            var activeUsers =
                await _userManager.Users
                    .CountAsync(u => u.IsActive);


            // =========================================
            // RECENT APPOINTMENTS
            // =========================================
            var recentAppointments =
                await _context.Appointments
                    .Include(a => a.Vehicle)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(5)
                    .ToListAsync();

            var customerNames =
                new Dictionary<string, string>();

            foreach (var appointment in recentAppointments)
            {
                if (!customerNames.ContainsKey(
                    appointment.CustomerId))
                {
                    var customer =
                        await _userManager.FindByIdAsync(
                            appointment.CustomerId);

                    customerNames[
                        appointment.CustomerId] =
                        customer?.FullName
                        ?? customer?.UserName
                        ?? customer?.Email
                        ?? "Unknown Customer";
                }
            }


            // =========================================
            // RECENT REPAIRS
            // =========================================
            var recentRepairs =
                await _context.Repairs
                    .Include(r => r.Appointment)
                        .ThenInclude(a => a!.Vehicle)
                    .OrderByDescending(r =>
                        r.UpdatedAt)
                    .Take(5)
                    .ToListAsync();

            var mechanicNames =
                new Dictionary<string, string>();

            foreach (var repair in recentRepairs)
            {
                if (!mechanicNames.ContainsKey(
                    repair.MechanicId))
                {
                    var mechanic =
                        await _userManager.FindByIdAsync(
                            repair.MechanicId);

                    mechanicNames[
                        repair.MechanicId] =
                        mechanic?.FullName
                        ?? mechanic?.UserName
                        ?? "Unknown Mechanic";
                }
            }


            // =========================================
            // SEND DATA TO VIEW
            // =========================================
            ViewBag.TotalAppointments =
                totalAppointments;

            ViewBag.PendingAppointments =
                pendingAppointments;

            ViewBag.ApprovedAppointments =
                approvedAppointments;

            ViewBag.TotalRepairs =
                totalRepairs;

            ViewBag.ActiveRepairs =
                activeRepairs;

            ViewBag.CompletedRepairs =
                completedRepairs;

            ViewBag.PendingAIDiagnostics =
                pendingAIDiagnostics;

            ViewBag.TotalInvoices =
                totalInvoices;

            ViewBag.UnpaidInvoices =
                unpaidInvoices;

            ViewBag.OutstandingBalance =
                outstandingBalance;

            ViewBag.TotalUsers =
                totalUsers;

            ViewBag.ActiveUsers =
                activeUsers;

            ViewBag.RecentAppointments =
                recentAppointments;

            ViewBag.CustomerNames =
                customerNames;

            ViewBag.RecentRepairs =
                recentRepairs;

            ViewBag.MechanicNames =
                mechanicNames;

            return View();
        }


        // =========================
        // USER MANAGEMENT
        // =========================
        public async Task<IActionResult> Users()
        {
            var allUsers = await _userManager.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var userRoles =
                new Dictionary<string, string>();

            var decryptedPhoneNumbers =
                new Dictionary<string, string>();

            foreach (var user in allUsers)
            {
                var roles =
                    await _userManager.GetRolesAsync(user);

                userRoles[user.Id] =
                    roles.FirstOrDefault() ?? "No Role";

                decryptedPhoneNumbers[user.Id] =
                    _encryptionService.Decrypt(
                        user.PhoneNumber) ?? "-";
            }

            ViewBag.UserRoles =
                userRoles;

            ViewBag.DecryptedPhoneNumbers =
                decryptedPhoneNumbers;

            return View(allUsers);
        }

        // =========================
        // DEACTIVATE USER
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateUser(
            string id)
        {
            var currentAdminId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(currentAdminId))
            {
                return Unauthorized();
            }

            var user =
                await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            // Prevent admin from deactivating own account
            if (user.Id == currentAdminId)
            {
                TempData["ErrorMessage"] =
                    "You cannot deactivate your own account.";

                return RedirectToAction(nameof(Users));
            }

            if (!user.IsActive)
            {
                TempData["ErrorMessage"] =
                    "This account is already inactive.";

                return RedirectToAction(nameof(Users));
            }

            user.IsActive = false;

            var result =
                await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] =
                    "Unable to deactivate the account.";

                return RedirectToAction(nameof(Users));
            }

            // Invalidate existing login sessions
            await _userManager.UpdateSecurityStampAsync(user);

            await _auditLogService.LogAsync(
                 User,
                "DEACTIVATE USER",
                "User",
                user.Id,
                 $"Deactivated account {user.UserName}."
);

            TempData["SuccessMessage"] =
                $"{user.FullName}'s account has been deactivated.";

            return RedirectToAction(nameof(Users));
        }


        // =========================
        // ACTIVATE USER
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateUser(
            string id)
        {
            var user =
                await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (user.IsActive)
            {
                TempData["ErrorMessage"] =
                    "This account is already active.";

                return RedirectToAction(nameof(Users));
            }

            user.IsActive = true;

            var result =
                await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] =
                    "Unable to activate the account.";

                return RedirectToAction(nameof(Users));
            }

            await _userManager.UpdateSecurityStampAsync(user);

            await _auditLogService.LogAsync(
                User,
                "ACTIVATE USER",
                "User",
                user.Id,
                $"Activated account {user.UserName}."
);

            TempData["SuccessMessage"] =
                $"{user.FullName}'s account has been activated.";

            return RedirectToAction(nameof(Users));
        }


        // =========================
        // ADD MECHANIC - GET
        // =========================
        [HttpGet]
        public IActionResult AddMechanic()
        {
            return View();
        }


        // =========================
        // ADD MECHANIC - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMechanic(
            AddMechanicViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username =
                model.Username.Trim();

            var email =
                model.Email.Trim();

            var existingUsername =
                await _userManager.FindByNameAsync(
                    username);

            if (existingUsername != null)
            {
                ModelState.AddModelError(
                    nameof(model.Username),
                    "This username is already taken.");

                return View(model);
            }

            var existingEmail =
                await _userManager.FindByEmailAsync(
                    email);

            if (existingEmail != null)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "An account with this email already exists.");

                return View(model);
            }

            var mechanic =
                new ApplicationUser
                {
                    FullName =
                        model.FullName.Trim(),

                    UserName =
                        username,

                    Email =
                        email,

                    PhoneNumber =
                        _encryptionService.Encrypt(
                            model.PhoneNumber?.Trim()),

                    EmailConfirmed =
                        true,

                    IsActive =
                        true,

                    CreatedAt =
                        DateTime.Now
                };

            var createResult =
                await _userManager.CreateAsync(
                    mechanic,
                    model.Password);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(model);
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    mechanic,
                    "Mechanic");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(
                    mechanic);

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(model);
            }

            await _auditLogService.LogAsync(
    User,
    "ADD MECHANIC",
    "User",
    mechanic.Id,
    $"Created mechanic account {mechanic.UserName}."
);

            TempData["SuccessMessage"] =
                $"{mechanic.FullName} has been added as a mechanic.";

            return RedirectToAction(
                nameof(Users));
        }


        // =========================
        // VEHICLE SERVICE HISTORY
        // =========================
        [HttpGet]
        public async Task<IActionResult> ServiceHistory(
            string? plateNumber)
        {
            var model = new ServiceHistoryViewModel();

            if (string.IsNullOrWhiteSpace(plateNumber))
            {
                return View(model);
            }

            model.SearchPerformed = true;
            model.SearchPlateNumber = plateNumber.Trim();

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.PlateNumber.ToLower() ==
                    model.SearchPlateNumber.ToLower());

            if (vehicle == null)
            {
                return View(model);
            }

            model.Vehicle = vehicle;

            model.Customer =
                await _userManager.FindByIdAsync(
                    vehicle.UserId);

            var appointments =
                await _context.Appointments
                    .Where(a =>
                        a.VehicleId == vehicle.VehicleId)
                    .OrderByDescending(a =>
                        a.AppointmentDate)
                    .ThenByDescending(a =>
                        a.AppointmentTime)
                    .ToListAsync();

            foreach (var appointment in appointments)
            {
                var repair =
                    await _context.Repairs
                        .FirstOrDefaultAsync(r =>
                            r.AppointmentId ==
                            appointment.AppointmentId);

                var mechanicName = "-";

                if (repair != null)
                {
                    var mechanic =
                        await _userManager.FindByIdAsync(
                            repair.MechanicId);

                    mechanicName =
                        mechanic?.FullName
                        ?? mechanic?.UserName
                        ?? "-";
                }

                var historyItem =
                    new ServiceHistoryItemViewModel
                    {
                        AppointmentId =
                            appointment.AppointmentId,

                        RepairId =
                            repair?.RepairId,

                        AppointmentDate =
                            appointment.AppointmentDate,

                        AppointmentTime =
                            appointment.AppointmentTime,

                        ServiceType =
                            appointment.ServiceType,

                        CustomerComplaint =
                            appointment.CustomerComplaint,

                        AppointmentStatus =
                            appointment.AppointmentStatus,

                        RepairStatus =
                            repair?.RepairStatus ?? "-",

                        IssueFound =
                            string.IsNullOrWhiteSpace(
                                repair?.Diagnosis)
                            ? "-"
                            : repair.Diagnosis,

                        ActionTaken =
                            string.IsNullOrWhiteSpace(
                                repair?.RepairNotes)
                            ? "-"
                            : repair.RepairNotes,

                        MechanicName =
                            mechanicName,

                        CompletedAt =
                            repair?.CompletedAt
                    };

                model.ServiceHistory.Add(
                    historyItem);
            }

            return View(model);
        }

        // =====================================================
        // CUSTOMER SUPPORT MESSAGES
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> CustomerMessages()
        {
            var adminId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }


            // =============================================
            // GET ALL SUPPORT CONVERSATIONS
            // =============================================

            var conversations = await _context.SupportConversations
                .Include(c => c.Customer)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();


            // =============================================
            // LATEST MESSAGE FOR EACH CONVERSATION
            // =============================================

            var latestMessages =
                new Dictionary<int, SupportMessage?>();


            // =============================================
            // UNREAD MESSAGE COUNT
            // =============================================

            var unreadCounts =
                new Dictionary<int, int>();


            foreach (var conversation in conversations)
            {
                var latestMessage =
                    await _context.SupportMessages
                        .Where(m =>
                            m.ConversationId ==
                            conversation.ConversationId)
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefaultAsync();


                latestMessages[
                    conversation.ConversationId] =
                    latestMessage;


                var unreadCount =
                    await _context.SupportMessages
                        .Where(m =>
                            m.ConversationId ==
                                conversation.ConversationId &&

                            m.SenderId != adminId &&

                            !_context.SupportMessageReads.Any(r =>
                                r.MessageId == m.MessageId &&
                                r.UserId == adminId))
                        .CountAsync();


                unreadCounts[
                    conversation.ConversationId] =
                    unreadCount;
            }


            ViewBag.LatestMessages =
                latestMessages;

            ViewBag.UnreadCounts =
                unreadCounts;

            return View(conversations);
        }

        // =========================
        // APPOINTMENT MANAGEMENT
        // =========================
        public async Task<IActionResult> Appointments()
        {
            var appointments =
                await _context.Appointments
                    .Include(a => a.Vehicle)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

            var customerEmails =
                new Dictionary<string, string>();

            foreach (var appointment in appointments)
            {
                if (!customerEmails.ContainsKey(
                    appointment.CustomerId))
                {
                    var customer =
                        await _userManager.FindByIdAsync(
                            appointment.CustomerId);

                    customerEmails[
                        appointment.CustomerId] =
                        customer?.Email ??
                        "Unknown Customer";
                }
            }

            ViewBag.CustomerEmails =
                customerEmails;

            return View(appointments);
        }


        // =========================
        // APPOINTMENT DETAILS
        // =========================
        public async Task<IActionResult> AppointmentDetails(
            int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment =
                await _context.Appointments
                    .Include(a => a.Vehicle)
                    .FirstOrDefaultAsync(a =>
                        a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var customer =
                await _userManager.FindByIdAsync(
                    appointment.CustomerId);

            ViewBag.CustomerEmail =
                customer?.Email ??
                "Unknown Customer";

            var repairExists =
                await _context.Repairs
                    .AnyAsync(r =>
                        r.AppointmentId ==
                        appointment.AppointmentId);

            ViewBag.RepairExists =
                repairExists;

            return View(appointment);
        }


        // =========================
        // APPROVE APPOINTMENT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAppointment(
            int id)
        {
            var appointment =
                await _context.Appointments
                    .FirstOrDefaultAsync(a =>
                        a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            if (appointment.AppointmentStatus !=
                "Pending")
            {
                TempData["ErrorMessage"] =
                    "Only pending appointments can be approved.";

                return RedirectToAction(
                    nameof(AppointmentDetails),
                    new { id });
            }

            appointment.AppointmentStatus =
                "Approved";

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                User,
                "APPROVE APPOINTMENT",
                "Appointment",
                appointment.AppointmentId.ToString(),
                $"Approved appointment #{appointment.AppointmentId}."
            );

            // Notify customer
            await _notificationService.CreateAsync(
                appointment.CustomerId,
                "Appointment Approved",
                $"Your appointment #{appointment.AppointmentId} has been approved.",
                "Appointment",
                $"/Appointment/Details/{appointment.AppointmentId}"
                );


            TempData["SuccessMessage"] =
                "Appointment approved successfully.";

            return RedirectToAction(
                nameof(AppointmentDetails),
                new { id });
        }


        // =========================
        // REJECT APPOINTMENT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAppointment(
            int id)
        {
            var appointment =
                await _context.Appointments
                    .FirstOrDefaultAsync(a =>
                        a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            if (appointment.AppointmentStatus !=
                "Pending")
            {
                TempData["ErrorMessage"] =
                    "Only pending appointments can be rejected.";

                return RedirectToAction(
                    nameof(AppointmentDetails),
                    new { id });
            }

            appointment.AppointmentStatus =
                "Rejected";

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                User,
                "REJECT APPOINTMENT",
                "Appointment",
                appointment.AppointmentId.ToString(),
                $"Rejected appointment #{appointment.AppointmentId}."
            );

            // Notify customer
            await _notificationService.CreateAsync(
                appointment.CustomerId,
                "Appointment Rejected",
                $"Your appointment #{appointment.AppointmentId} has been rejected.",
                "Appointment",
                $"/Appointment/Details/{appointment.AppointmentId}"
            );

            TempData["SuccessMessage"] =
                "Appointment rejected successfully.";

            return RedirectToAction(
                nameof(AppointmentDetails),
                new { id });
        }


        // =========================
        // ADMIN REPAIR MANAGEMENT
        // =========================
        [HttpGet]
        public async Task<IActionResult> Repairs()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var repairs =
                await _context.Repairs
                    .Include(r => r.Appointment)
                    .ThenInclude(a => a!.Vehicle)
                    .OrderByDescending(r =>
                        r.CreatedAt)
                    .ToListAsync();

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
        // CREATE REPAIR - GET
        // =========================
        [HttpGet]
        public async Task<IActionResult> CreateRepair(
            int appointmentId)
        {
            var appointment =
                await _context.Appointments
                    .Include(a => a.Vehicle)
                    .FirstOrDefaultAsync(a =>
                        a.AppointmentId ==
                        appointmentId);

            if (appointment == null)
            {
                return NotFound();
            }

            if (appointment.AppointmentStatus !=
                "Approved")
            {
                TempData["ErrorMessage"] =
                    "Only approved appointments can be converted into repairs.";

                return RedirectToAction(
                    nameof(AppointmentDetails),
                    new { id = appointmentId });
            }

            var repairExists =
                await _context.Repairs
                    .AnyAsync(r =>
                        r.AppointmentId ==
                        appointmentId);

            if (repairExists)
            {
                TempData["ErrorMessage"] =
                    "A repair record already exists for this appointment.";

                return RedirectToAction(
                    nameof(AppointmentDetails),
                    new { id = appointmentId });
            }

            var mechanics =
                await _userManager.GetUsersInRoleAsync(
                    "Mechanic");

            var activeMechanics =
                mechanics
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.FullName)
                    .ToList();

            ViewBag.Mechanics =
                new SelectList(
                    activeMechanics,
                    "Id",
                    "FullName");

            ViewBag.Appointment =
                appointment;

            return View();
        }


        // =========================
        // CREATE REPAIR - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRepair(
            int appointmentId,
            string mechanicId)
        {
            var appointment =
                await _context.Appointments
                    .FirstOrDefaultAsync(a =>
                        a.AppointmentId ==
                        appointmentId);

            if (appointment == null)
            {
                return NotFound();
            }

            if (appointment.AppointmentStatus !=
                "Approved")
            {
                TempData["ErrorMessage"] =
                    "Only approved appointments can be converted into repairs.";

                return RedirectToAction(
                    nameof(AppointmentDetails),
                    new { id = appointmentId });
            }

            var repairExists =
                await _context.Repairs
                    .AnyAsync(r =>
                        r.AppointmentId ==
                        appointmentId);

            if (repairExists)
            {
                TempData["ErrorMessage"] =
                    "A repair record already exists for this appointment.";

                return RedirectToAction(
                    nameof(AppointmentDetails),
                    new { id = appointmentId });
            }

            var mechanic =
                await _userManager.FindByIdAsync(
                    mechanicId);

            if (mechanic == null ||
                !mechanic.IsActive ||
                !await _userManager.IsInRoleAsync(
                    mechanic,
                    "Mechanic"))
            {
                TempData["ErrorMessage"] =
                    "Please select a valid active mechanic.";

                return RedirectToAction(
                    nameof(CreateRepair),
                    new { appointmentId });
            }

            var adminId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var repair =
                new Repair
                {
                    AppointmentId =
                        appointmentId,

                    MechanicId =
                        mechanicId,

                    RepairStatus =
                        "Assigned",

                    CreatedAt =
                        DateTime.Now,

                    UpdatedAt =
                        DateTime.Now
                };

            _context.Repairs.Add(
                repair);

            await _context.SaveChangesAsync();

            var progress =
                new RepairProgress
                {
                    RepairId =
                        repair.RepairId,

                    Status =
                        "Assigned",

                    Description =
                        "Repair assigned to mechanic.",

                    UpdatedBy =
                        adminId,

                    CreatedAt =
                        DateTime.Now
                };

            _context.RepairProgresses.Add(
                progress);

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
    User,
    "CREATE REPAIR",
    "Repair",
    repair.RepairId.ToString(),
    $"Created repair #{repair.RepairId} from appointment #{appointmentId} and assigned mechanic {mechanic.FullName}."
);

            // Notify assigned mechanic
            await _notificationService.CreateAsync(
                mechanicId,
                "New Repair Assigned",
                $"Repair #{repair.RepairId} has been assigned to you.",
                "Repair",
                $"/Mechanic/RepairDetails/{repair.RepairId}"
            );

            TempData["SuccessMessage"] =
                "Repair created and mechanic assigned successfully.";

            return RedirectToAction(
                nameof(Repairs));
        }


        // =====================================================
        // AI DIAGNOSTIC MANAGEMENT
        // =====================================================

        // =========================
        // AI DIAGNOSTIC LIST
        // =========================
        [HttpGet]
        public async Task<IActionResult> AIDiagnostics()
        {
            var diagnostics =
                await _context.AIDiagnostics
                    .Include(d => d.Vehicle)
                    .OrderBy(d =>
                        d.ReviewStatus ==
                        "Pending Assignment"
                            ? 0
                            : d.ReviewStatus ==
                              "Pending Review"
                                ? 1
                                : 2)
                    .ThenByDescending(d =>
                        d.CreatedAt)
                    .ToListAsync();

            var customerNames =
                new Dictionary<string, string>();

            var mechanicNames =
                new Dictionary<string, string>();

            foreach (var diagnostic in diagnostics)
            {
                if (!customerNames.ContainsKey(
                    diagnostic.CustomerId))
                {
                    var customer =
                        await _userManager.FindByIdAsync(
                            diagnostic.CustomerId);

                    customerNames[
                        diagnostic.CustomerId] =
                        customer?.FullName
                        ?? customer?.UserName
                        ?? customer?.Email
                        ?? "Unknown Customer";
                }

                if (!string.IsNullOrEmpty(
                    diagnostic.AssignedMechanicId) &&
                    !mechanicNames.ContainsKey(
                        diagnostic.AssignedMechanicId))
                {
                    var mechanic =
                        await _userManager.FindByIdAsync(
                            diagnostic.AssignedMechanicId);

                    mechanicNames[
                        diagnostic.AssignedMechanicId] =
                        mechanic?.FullName
                        ?? mechanic?.UserName
                        ?? "Unknown Mechanic";
                }
            }

            ViewBag.CustomerNames =
                customerNames;

            ViewBag.MechanicNames =
                mechanicNames;

            return View(diagnostics);
        }


        // =========================
        // AI DIAGNOSTIC DETAILS
        // =========================
        [HttpGet]
        public async Task<IActionResult>
            AIDiagnosticDetails(int id)
        {
            var diagnostic =
                await _context.AIDiagnostics
                    .Include(d => d.Vehicle)
                    .FirstOrDefaultAsync(d =>
                        d.DiagnosticId == id);

            if (diagnostic == null)
            {
                return NotFound();
            }

            var customer =
                await _userManager.FindByIdAsync(
                    diagnostic.CustomerId);

            ViewBag.CustomerName =
                customer?.FullName
                ?? customer?.UserName
                ?? customer?.Email
                ?? "Unknown Customer";

            if (!string.IsNullOrEmpty(
                diagnostic.AssignedMechanicId))
            {
                var assignedMechanic =
                    await _userManager.FindByIdAsync(
                        diagnostic.AssignedMechanicId);

                ViewBag.AssignedMechanicName =
                    assignedMechanic?.FullName
                    ?? assignedMechanic?.UserName
                    ?? "Unknown Mechanic";
            }
            else
            {
                ViewBag.AssignedMechanicName =
                    "Not Assigned";
            }

            var mechanics =
                await _userManager.GetUsersInRoleAsync(
                    "Mechanic");

            var activeMechanics =
                mechanics
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.FullName)
                    .ToList();

            ViewBag.Mechanics =
                new SelectList(
                    activeMechanics,
                    "Id",
                    "FullName",
                    diagnostic.AssignedMechanicId);

            return View(diagnostic);
        }


        // =========================
        // ASSIGN AI DIAGNOSTIC
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            AssignAIDiagnosticMechanic(
                int id,
                string mechanicId)
        {
            var diagnostic =
                await _context.AIDiagnostics
                    .FirstOrDefaultAsync(d =>
                        d.DiagnosticId == id);

            if (diagnostic == null)
            {
                return NotFound();
            }

            if (diagnostic.ReviewStatus ==
                    "Confirmed" ||
                diagnostic.ReviewStatus ==
                    "Modified")
            {
                TempData["ErrorMessage"] =
                    "A completed AI diagnostic review cannot be reassigned.";

                return RedirectToAction(
                    nameof(AIDiagnosticDetails),
                    new { id });
            }

            if (string.IsNullOrWhiteSpace(
                mechanicId))
            {
                TempData["ErrorMessage"] =
                    "Please select a mechanic.";

                return RedirectToAction(
                    nameof(AIDiagnosticDetails),
                    new { id });
            }

            var mechanic =
                await _userManager.FindByIdAsync(
                    mechanicId);

            if (mechanic == null ||
                !mechanic.IsActive ||
                !await _userManager.IsInRoleAsync(
                    mechanic,
                    "Mechanic"))
            {
                TempData["ErrorMessage"] =
                    "Please select a valid active mechanic.";

                return RedirectToAction(
                    nameof(AIDiagnosticDetails),
                    new { id });
            }

            diagnostic.AssignedMechanicId =
                mechanic.Id;

            diagnostic.ReviewStatus =
                "Pending Review";

            diagnostic.ReviewedBy =
                null;

            diagnostic.MechanicComments =
                null;

            diagnostic.ReviewedAt =
                null;

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
    User,
    "ASSIGN AI DIAGNOSTIC",
    "AIDiagnostic",
    diagnostic.DiagnosticId.ToString(),
    $"Assigned AI diagnostic #{diagnostic.DiagnosticId} to mechanic {mechanic.FullName}."
);

            // Notify assigned mechanic
            await _notificationService.CreateAsync(
                mechanic.Id,
                "AI Diagnostic Assigned",
                $"AI diagnostic #{diagnostic.DiagnosticId} has been assigned to you for review.",
                "AIDiagnostic",
                $"/Mechanic/AIDiagnosticDetails/{diagnostic.DiagnosticId}"
            );


            TempData["SuccessMessage"] =
                $"AI diagnostic assigned to " +
                $"{mechanic.FullName} successfully.";

            return RedirectToAction(
                nameof(AIDiagnosticDetails),
                new { id });
        }


        // =====================================================
        // INVOICE MANAGEMENT
        // =====================================================

        // =========================
        // INVOICE LIST
        // =========================
        [HttpGet]
        public async Task<IActionResult> Invoices()
        {
            var invoices =
                await _context.Invoices
                    .Include(i => i.Repair)
                        .ThenInclude(r => r!.Appointment)
                        .ThenInclude(a => a!.Vehicle)
                    .OrderByDescending(i =>
                        i.InvoiceDate)
                    .ThenByDescending(i =>
                        i.InvoiceId)
                    .ToListAsync();

            var customerNames =
                new Dictionary<string, string>();

            foreach (var invoice in invoices)
            {
                var customerId =
                    invoice.Repair?
                        .Appointment?
                        .CustomerId;

                if (!string.IsNullOrEmpty(customerId) &&
                    !customerNames.ContainsKey(customerId))
                {
                    var customer =
                        await _userManager.FindByIdAsync(
                            customerId);

                    customerNames[customerId] =
                        customer?.FullName
                        ?? customer?.UserName
                        ?? customer?.Email
                        ?? "Unknown Customer";
                }
            }

            var invoicedRepairIds =
                await _context.Invoices
                    .Select(i => i.RepairId)
                    .ToListAsync();

            var completedRepairsReadyForInvoice =
                await _context.Repairs
                    .Include(r => r.Appointment)
                        .ThenInclude(a => a!.Vehicle)
                    .Where(r =>
                        r.RepairStatus == "Completed" &&
                        !invoicedRepairIds.Contains(
                            r.RepairId))
                    .OrderByDescending(r =>
                        r.CompletedAt)
                    .ToListAsync();

            var readyCustomerNames =
                new Dictionary<string, string>();

            foreach (var repair
                in completedRepairsReadyForInvoice)
            {
                var customerId =
                    repair.Appointment?.CustomerId;

                if (!string.IsNullOrEmpty(customerId) &&
                    !readyCustomerNames.ContainsKey(
                        customerId))
                {
                    var customer =
                        await _userManager.FindByIdAsync(
                            customerId);

                    readyCustomerNames[customerId] =
                        customer?.FullName
                        ?? customer?.UserName
                        ?? customer?.Email
                        ?? "Unknown Customer";
                }
            }

            ViewBag.CustomerNames =
                customerNames;

            ViewBag.CompletedRepairsReadyForInvoice =
                completedRepairsReadyForInvoice;

            ViewBag.ReadyCustomerNames =
                readyCustomerNames;

            return View(invoices);
        }


        // =========================
        // CREATE INVOICE - GET
        // =========================
        [HttpGet]
        public async Task<IActionResult> CreateInvoice(
            int repairId)
        {
            var repair =
                await _context.Repairs
                    .Include(r => r.Appointment)
                        .ThenInclude(a => a!.Vehicle)
                    .FirstOrDefaultAsync(r =>
                        r.RepairId == repairId);

            if (repair == null)
            {
                return NotFound();
            }

            if (repair.RepairStatus != "Completed")
            {
                TempData["ErrorMessage"] =
                    "An invoice can only be created for a completed repair.";

                return RedirectToAction(
                    nameof(Invoices));
            }

            var invoiceExists =
                await _context.Invoices
                    .AnyAsync(i =>
                        i.RepairId == repairId);

            if (invoiceExists)
            {
                TempData["ErrorMessage"] =
                    "An invoice already exists for this repair.";

                return RedirectToAction(
                    nameof(Invoices));
            }

            if (repair.Appointment == null ||
                repair.Appointment.Vehicle == null)
            {
                TempData["ErrorMessage"] =
                    "The repair does not contain complete appointment or vehicle information.";

                return RedirectToAction(
                    nameof(Invoices));
            }

            var customer =
                await _userManager.FindByIdAsync(
                    repair.Appointment.CustomerId);

            var vehicle =
                repair.Appointment.Vehicle;

            var model =
                new CreateInvoiceViewModel
                {
                    RepairId =
                        repair.RepairId,

                    CustomerName =
                        customer?.FullName
                        ?? customer?.UserName
                        ?? customer?.Email
                        ?? "Unknown Customer",

                    VehicleName =
                        $"{vehicle.Brand} {vehicle.Model}",

                    PlateNumber =
                        vehicle.PlateNumber,

                    Mileage =
                        vehicle.CurrentMileage,

                    IssueFound =
                        repair.Diagnosis,

                    ActionTaken =
                        repair.RepairNotes,

                    Remarks =
                        repair.Appointment.CustomerComplaint,

                    Items =
                        new List<CreateInvoiceItemViewModel>
                        {
                            new CreateInvoiceItemViewModel
                            {
                                Quantity = 1
                            }
                        }
                };

            return View(model);
        }


        // =========================
        // CREATE INVOICE - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoice(
            CreateInvoiceViewModel model)
        {
            var adminId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var repair =
                await _context.Repairs
                    .Include(r => r.Appointment)
                        .ThenInclude(a => a!.Vehicle)
                    .FirstOrDefaultAsync(r =>
                        r.RepairId == model.RepairId);

            if (repair == null)
            {
                return NotFound();
            }

            if (repair.RepairStatus != "Completed")
            {
                TempData["ErrorMessage"] =
                    "An invoice can only be created for a completed repair.";

                return RedirectToAction(
                    nameof(Invoices));
            }

            var invoiceExists =
                await _context.Invoices
                    .AnyAsync(i =>
                        i.RepairId ==
                        model.RepairId);

            if (invoiceExists)
            {
                TempData["ErrorMessage"] =
                    "An invoice already exists for this repair.";

                return RedirectToAction(
                    nameof(Invoices));
            }

            if (model.Items == null ||
                model.Items.Count == 0)
            {
                ModelState.AddModelError(
                    nameof(model.Items),
                    "Please add at least one invoice item.");
            }
            else
            {
                var allowedItemTypes =
                    new[]
                    {
                        "Part",
                        "Labour",
                        "Service",
                        "Other"
                    };

                for (var i = 0;
                    i < model.Items.Count;
                    i++)
                {
                    var item =
                        model.Items[i];

                    if (string.IsNullOrWhiteSpace(
                        item.Description))
                    {
                        ModelState.AddModelError(
                            $"Items[{i}].Description",
                            "Description is required.");
                    }

                    if (!allowedItemTypes.Contains(
                        item.ItemType))
                    {
                        ModelState.AddModelError(
                            $"Items[{i}].ItemType",
                            "Please select a valid item type.");
                    }

                    if (item.Quantity <= 0)
                    {
                        ModelState.AddModelError(
                            $"Items[{i}].Quantity",
                            "Quantity must be greater than zero.");
                    }

                    if (item.UnitPrice < 0)
                    {
                        ModelState.AddModelError(
                            $"Items[{i}].UnitPrice",
                            "Unit price cannot be negative.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateCreateInvoiceDisplayAsync(
                    model,
                    repair);

                return View(model);
            }

            var invoiceItems =
                model.Items
                    .Select(item =>
                        new InvoiceItem
                        {
                            Description =
                                item.Description.Trim(),

                            ItemType =
                                item.ItemType,

                            Quantity =
                                item.Quantity,

                            UnitPrice =
                                item.UnitPrice,

                            Amount =
                                Math.Round(
                                    item.Quantity *
                                    item.UnitPrice,
                                    2,
                                    MidpointRounding
                                        .AwayFromZero)
                        })
                    .ToList();

            var subtotal =
                invoiceItems.Sum(
                    item => item.Amount);

            var now =
                DateTime.Now;

            var todayStart =
                now.Date;

            var tomorrowStart =
                todayStart.AddDays(1);

            var invoicesToday =
                await _context.Invoices
                    .CountAsync(i =>
                        i.CreatedAt >= todayStart &&
                        i.CreatedAt < tomorrowStart);

            var invoiceNumber =
                $"INV-{now:yyyyMMdd}-" +
                $"{invoicesToday + 1:000}";

            while (await _context.Invoices
                .AnyAsync(i =>
                    i.InvoiceNumber ==
                    invoiceNumber))
            {
                invoicesToday++;

                invoiceNumber =
                    $"INV-{now:yyyyMMdd}-" +
                    $"{invoicesToday + 1:000}";
            }

            var invoice =
                new Invoice
                {
                    InvoiceNumber =
                        invoiceNumber,

                    RepairId =
                        repair.RepairId,

                    InvoiceDate =
                        now,

                    Mileage =
                        model.Mileage,

                    Remarks =
                        string.IsNullOrWhiteSpace(
                            model.Remarks)
                        ? null
                        : model.Remarks.Trim(),

                    Subtotal =
                        subtotal,

                    TotalAmount =
                        subtotal,

                    PaidAmount =
                        0,

                    BalanceAmount =
                        subtotal,

                    PaymentStatus =
                        "Unpaid",

                    CreatedBy =
                        adminId,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now,

                    InvoiceItems =
                        invoiceItems
                };

            _context.Invoices.Add(
                invoice);

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
    User,
    "CREATE INVOICE",
    "Invoice",
    invoice.InvoiceId.ToString(),
    $"Created invoice {invoice.InvoiceNumber} for repair #{invoice.RepairId} with total RM {invoice.TotalAmount:N2}."
);

            // Notify customer
            if (repair.Appointment != null)
            {
                await _notificationService.CreateAsync(
                    repair.Appointment.CustomerId,
                    "New Invoice",
                    $"Invoice {invoice.InvoiceNumber} has been created. Total amount: RM {invoice.TotalAmount:N2}.",
                    "Invoice",
                    $"/Customer/InvoiceDetails/{invoice.InvoiceId}"
                );
            }

            TempData["SuccessMessage"] =
                $"Invoice {invoice.InvoiceNumber} created successfully.";

            return RedirectToAction(
                nameof(InvoiceDetails),
                new
                {
                    id = invoice.InvoiceId
                });
        }


        // =========================
        // INVOICE DETAILS
        // =========================
        [HttpGet]
        public async Task<IActionResult> InvoiceDetails(
            int id)
        {
            var invoice =
                await _context.Invoices
                    .Include(i =>
                        i.InvoiceItems)
                    .Include(i =>
                        i.Repair)
                        .ThenInclude(r =>
                            r!.Appointment)
                        .ThenInclude(a =>
                            a!.Vehicle)
                    .FirstOrDefaultAsync(i =>
                        i.InvoiceId == id);

            if (invoice == null)
            {
                return NotFound();
            }

            var customerId =
                invoice.Repair?
                    .Appointment?
                    .CustomerId;

            if (!string.IsNullOrEmpty(
                customerId))
            {
                var customer =
                    await _userManager.FindByIdAsync(
                        customerId);

                ViewBag.CustomerName =
                    customer?.FullName
                    ?? customer?.UserName
                    ?? customer?.Email
                    ?? "Unknown Customer";

                ViewBag.CustomerEmail =
                    customer?.Email ?? "-";

                ViewBag.CustomerPhone =
                    _encryptionService.Decrypt(
                        customer?.PhoneNumber) ?? "-";
            }
            else
            {
                ViewBag.CustomerName =
                    "Unknown Customer";

                ViewBag.CustomerEmail =
                    "-";

                ViewBag.CustomerPhone =
                    "-";
            }

            return View(invoice);
        }

        // =========================
        // RECORD INVOICE PAYMENT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(
            int invoiceId,
            decimal paymentAmount)
        {
            var invoice =
    await _context.Invoices
        .Include(i => i.Repair)
            .ThenInclude(r => r!.Appointment)
        .FirstOrDefaultAsync(i =>
            i.InvoiceId == invoiceId);

            if (invoice == null)
            {
                return NotFound();
            }

            // Invoice already fully paid
            if (invoice.PaymentStatus == "Paid" ||
                invoice.BalanceAmount <= 0)
            {
                TempData["ErrorMessage"] =
                    "This invoice has already been fully paid.";

                return RedirectToAction(
                    nameof(InvoiceDetails),
                    new { id = invoiceId });
            }

            // Payment must be greater than zero
            if (paymentAmount <= 0)
            {
                TempData["ErrorMessage"] =
                    "Payment amount must be greater than RM 0.00.";

                return RedirectToAction(
                    nameof(InvoiceDetails),
                    new { id = invoiceId });
            }

            // Prevent overpayment
            if (paymentAmount > invoice.BalanceAmount)
            {
                TempData["ErrorMessage"] =
                    $"Payment cannot exceed the outstanding balance of " +
                    $"RM {invoice.BalanceAmount:N2}.";

                return RedirectToAction(
                    nameof(InvoiceDetails),
                    new { id = invoiceId });
            }

            // Add payment to existing paid amount
            invoice.PaidAmount += paymentAmount;

            // Recalculate balance from total
            invoice.BalanceAmount =
                invoice.TotalAmount -
                invoice.PaidAmount;

            // Avoid tiny decimal remainder
            if (invoice.BalanceAmount <= 0)
            {
                invoice.BalanceAmount = 0;
                invoice.PaidAmount =
                    invoice.TotalAmount;

                invoice.PaymentStatus =
                    "Paid";
            }
            else if (invoice.PaidAmount > 0)
            {
                invoice.PaymentStatus =
                    "Partially Paid";
            }
            else
            {
                invoice.PaymentStatus =
                    "Unpaid";
            }

            invoice.UpdatedAt =
                DateTime.Now;

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
    User,
    "RECORD PAYMENT",
    "Invoice",
    invoice.InvoiceId.ToString(),
    $"Recorded payment of RM {paymentAmount:N2} for invoice {invoice.InvoiceNumber}. Remaining balance: RM {invoice.BalanceAmount:N2}."
);

            // Notify customer about payment update
            if (invoice.Repair?.Appointment != null)
            {
                if (invoice.PaymentStatus == "Paid")
                {
                    await _notificationService.CreateAsync(
                        invoice.Repair.Appointment.CustomerId,
                        "Invoice Paid",
                        $"Invoice {invoice.InvoiceNumber} has been fully paid.",
                        "Invoice",
                        $"/Customer/InvoiceDetails/{invoice.InvoiceId}"
                    );
                }
                else
                {
                    await _notificationService.CreateAsync(
                        invoice.Repair.Appointment.CustomerId,
                        "Payment Updated",
                        $"Payment of RM {paymentAmount:N2} has been recorded for invoice {invoice.InvoiceNumber}. Remaining balance: RM {invoice.BalanceAmount:N2}.",
                        "Invoice",
                        $"/Customer/InvoiceDetails/{invoice.InvoiceId}"
                    );
                }
            }

            TempData["SuccessMessage"] =
                $"Payment of RM {paymentAmount:N2} recorded successfully.";

            return RedirectToAction(
                nameof(InvoiceDetails),
                new { id = invoiceId });
        }

      



        // =========================
        // CREATE INVOICE HELPER
        // =========================
        private async Task
            PopulateCreateInvoiceDisplayAsync(
                CreateInvoiceViewModel model,
                Repair repair)
        {
            var appointment =
                repair.Appointment;

            var vehicle =
                appointment?.Vehicle;

            if (appointment != null)
            {
                var customer =
                    await _userManager.FindByIdAsync(
                        appointment.CustomerId);

                model.CustomerName =
                    customer?.FullName
                    ?? customer?.UserName
                    ?? customer?.Email
                    ?? "Unknown Customer";
            }
            else
            {
                model.CustomerName =
                    "Unknown Customer";
            }

            if (vehicle != null)
            {
                model.VehicleName =
                    $"{vehicle.Brand} {vehicle.Model}";

                model.PlateNumber =
                    vehicle.PlateNumber;

                if (!model.Mileage.HasValue)
                {
                    model.Mileage =
                        vehicle.CurrentMileage;
                }
            }

            model.IssueFound =
                repair.Diagnosis;

            model.ActionTaken =
                repair.RepairNotes;

            if (model.Items == null ||
                model.Items.Count == 0)
            {
                model.Items =
                    new List<CreateInvoiceItemViewModel>
                    {
                        new CreateInvoiceItemViewModel
                        {
                            Quantity = 1
                        }
                    };
            }
        }
    }
}