using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AIVehicleWorkshopSystem.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }


        // =========================
        // VEHICLE SERVICE SYSTEM
        // =========================

        public DbSet<Vehicle> Vehicles { get; set; }

        public DbSet<Appointment> Appointments { get; set; }

        public DbSet<Repair> Repairs { get; set; }

        public DbSet<RepairProgress> RepairProgresses { get; set; }


        // =========================
        // REPAIR CHAT
        // =========================

        public DbSet<ChatConversation> ChatConversations { get; set; }

        public DbSet<ChatMessage> ChatMessages { get; set; }

        public DbSet<ChatMessageRead> ChatMessageReads { get; set; }


        // =========================
        // CUSTOMER SUPPORT CHAT
        // =========================

        public DbSet<SupportConversation> SupportConversations { get; set; }

        public DbSet<SupportMessage> SupportMessages { get; set; }

        public DbSet<SupportMessageRead> SupportMessageReads { get; set; }


        // =========================
        // AI DIAGNOSTIC
        // =========================

        public DbSet<AIDiagnostic> AIDiagnostics { get; set; }


        // =========================
        // INVOICE
        // =========================

        public DbSet<Invoice> Invoices { get; set; }

        public DbSet<InvoiceItem> InvoiceItems { get; set; }


        // =========================
        // AUDIT LOG
        // =========================

        public DbSet<AuditLog> AuditLogs { get; set; }


        // =========================
        // NOTIFICATIONS
        // =========================

        public DbSet<Notification> Notifications { get; set; }


        // =========================
        // PUSH NOTIFICATIONS
        // =========================

        public DbSet<PushSubscription> PushSubscriptions { get; set; }


        // =========================
        // DATABASE CONFIGURATION
        // =========================

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // =============================================
            // VEHICLE OWNER RELATIONSHIP
            // One user can register multiple vehicles
            // =============================================

            builder.Entity<Vehicle>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Restrict);


            // =============================================
            // REPAIR CHAT
            // Prevent the same user from creating
            // duplicate read records for the same message
            // =============================================

            builder.Entity<ChatMessageRead>()
                .HasIndex(r => new
                {
                    r.MessageId,
                    r.UserId
                })
                .IsUnique();


            // =============================================
            // CUSTOMER SUPPORT CHAT
            // One support conversation for each customer
            // =============================================

            builder.Entity<SupportConversation>()
                .HasIndex(c => c.CustomerId)
                .IsUnique();


            // =============================================
            // CUSTOMER SUPPORT CHAT READ RECEIPTS
            // Prevent duplicate read records
            // =============================================

            builder.Entity<SupportMessageRead>()
                .HasIndex(r => new
                {
                    r.MessageId,
                    r.UserId
                })
                .IsUnique();


            // =============================================
            // PUSH NOTIFICATIONS
            // Prevent the same browser/device subscription
            // from being stored more than once
            // =============================================

            builder.Entity<PushSubscription>()
                .HasIndex(p => p.Endpoint)
                .IsUnique();


            // =============================================
            // PUSH NOTIFICATION USER RELATIONSHIP
            // One user can have multiple devices
            // =============================================

            builder.Entity<PushSubscription>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}