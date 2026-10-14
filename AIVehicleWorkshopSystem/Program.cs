using AIVehicleWorkshopSystem.Data;
using AIVehicleWorkshopSystem.Models;
using AIVehicleWorkshopSystem.Services;
using AIVehicleWorkshopSystem.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;


var builder = WebApplication.CreateBuilder(args);


// =========================
// DATABASE CONNECTION
// =========================
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found."
    );

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();


// =========================
// ASP.NET IDENTITY + ROLES
// =========================
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();


// =========================
// MVC
// =========================
builder.Services.AddControllersWithViews();


// =========================
// ANTI-FORGERY
// =========================
// Used by AJAX/fetch POST requests such as Repair Chat.
// The token remains validated by [ValidateAntiForgeryToken].
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});


// =========================
// SIGNALR
// =========================
builder.Services.AddSignalR();


// =========================
// DATA PROTECTION
// =========================
builder.Services.AddDataProtection();


// =========================
// APPLICATION SERVICES
// =========================
builder.Services.AddScoped<DataEncryptionService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<WebPushNotificationService>();


// =========================
// EMAIL SERVICE
// =========================
builder.Services.AddTransient<IEmailSender, AzureEmailSender>();


// =========================
// HTTP CLIENT SERVICES
// =========================
builder.Services.AddHttpClient<AIDiagnosticService>();


var app = builder.Build();


// =========================
// SEED ROLES
// =========================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    await RoleSeeder.SeedRolesAsync(services);
}


// =========================
// HTTP REQUEST PIPELINE
// =========================
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();


// =========================
// AUTHENTICATION
// =========================
app.UseAuthentication();


// =========================
// AUTHORIZATION
// =========================
app.UseAuthorization();


app.MapStaticAssets();


// =========================
// MVC ROUTE
// =========================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


// =========================
// SIGNALR HUBS
// =========================

// Repair Chat
app.MapHub<ChatHub>("/chatHub");

// Customer ↔ Admin Support Chat
app.MapHub<SupportChatHub>("/supportChatHub");

// Real-Time Notifications
app.MapHub<NotificationHub>("/notificationHub");


// =========================
// IDENTITY RAZOR PAGES
// =========================
app.MapRazorPages()
    .WithStaticAssets();


app.Run();