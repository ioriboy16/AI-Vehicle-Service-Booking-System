using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Identity;

namespace AIVehicleWorkshopSystem.Data
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(
            IServiceProvider serviceProvider)
        {
            var roleManager =
                serviceProvider
                    .GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                serviceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();

            var environment =
                serviceProvider
                    .GetRequiredService<IWebHostEnvironment>();

            var configuration =
                serviceProvider
                    .GetRequiredService<IConfiguration>();

            // =========================
            // CREATE SYSTEM ROLES
            // =========================
            string[] roles =
            {
                "Customer",
                "Mechanic",
                "Admin"
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult =
                        await roleManager.CreateAsync(
                            new IdentityRole(role)
                        );

                    if (!roleResult.Succeeded)
                    {
                        var errors = string.Join(
                            ", ",
                            roleResult.Errors
                                .Select(e => e.Description)
                        );

                        throw new Exception(
                            $"Failed to create {role} role: {errors}"
                        );
                    }
                }
            }

            // =========================
            // DEVELOPMENT ACCOUNTS
            // =========================
            // These accounts are only created when
            // running locally in Development mode.
            if (environment.IsDevelopment())
            {
                await CreateUserWithRoleAsync(
                    userManager,
                    fullName: "System Administrator",
                    username: "admin",
                    email: "admin@serishautocare.com",
                    password: "Admin123!",
                    role: "Admin"
                );

                await CreateUserWithRoleAsync(
                    userManager,
                    fullName: "Test Mechanic",
                    username: "mechanic",
                    email: "mechanic@serishautocare.com",
                    password: "Mechanic123!",
                    role: "Mechanic"
                );
            }

            // =========================
            // PRODUCTION ACCOUNTS
            // =========================
            // Credentials are read from environment
            // variables / Azure App Settings.
            if (!environment.IsDevelopment())
            {
                
                string? adminEmail =
                    configuration["SEED_ADMIN_EMAIL"];

                string? adminPassword =
                    configuration["SEED_ADMIN_PASSWORD"];

                string? mechanicEmail =
                    configuration["SEED_MECHANIC_EMAIL"];

                string? mechanicPassword =
                    configuration["SEED_MECHANIC_PASSWORD"];

                // -------------------------
                // CREATE PRODUCTION ADMIN
                // -------------------------
                if (!string.IsNullOrWhiteSpace(adminEmail) &&
                    !string.IsNullOrWhiteSpace(adminPassword))
                {
                    await CreateUserWithRoleAsync(
                        userManager,
                        fullName: "System Administrator",
                        username: "admin",
                        email: adminEmail,
                        password: adminPassword,
                        role: "Admin"
                    );
                }

                // -------------------------
                // CREATE PRODUCTION MECHANIC
                // -------------------------
                if (!string.IsNullOrWhiteSpace(mechanicEmail) &&
                    !string.IsNullOrWhiteSpace(mechanicPassword))
                {
                    await CreateUserWithRoleAsync(
                        userManager,
                        fullName: "Test Mechanic",
                        username: "mechanic",
                        email: mechanicEmail,
                        password: mechanicPassword,
                        role: "Mechanic"
                    );
                }
            }

            // =========================
            // EXISTING USERS
            // =========================
            var users = userManager.Users.ToList();

            foreach (var user in users)
            {
                bool userUpdated = false;

                // -------------------------
                // BACKFILL FULL NAME
                // -------------------------
                if (string.IsNullOrWhiteSpace(user.FullName))
                {
                    user.FullName =
                        !string.IsNullOrWhiteSpace(user.UserName)
                            ? user.UserName
                            : "User";

                    userUpdated = true;
                }

                // -------------------------
                // SET CREATED DATE
                // -------------------------
                if (user.CreatedAt == default)
                {
                    user.CreatedAt = DateTime.Now;
                    userUpdated = true;
                }

                if (userUpdated)
                {
                    var updateResult =
                        await userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        var errors = string.Join(
                            ", ",
                            updateResult.Errors
                                .Select(e => e.Description)
                        );

                        throw new Exception(
                            $"Failed to update user {user.Email}: {errors}"
                        );
                    }
                }

                // -------------------------
                // ASSIGN ROLE IF NONE
                // -------------------------
                var userRoles =
                    await userManager.GetRolesAsync(user);

                if (!userRoles.Any())
                {
                    var roleResult =
                        await userManager.AddToRoleAsync(
                            user,
                            "Customer"
                        );

                    if (!roleResult.Succeeded)
                    {
                        var errors = string.Join(
                            ", ",
                            roleResult.Errors
                                .Select(e => e.Description)
                        );

                        throw new Exception(
                            $"Failed to assign Customer role: {errors}"
                        );
                    }
                }

                // -------------------------
                // UPDATE OLD USERNAME
                // -------------------------
                // Previously username = email.
                if (!string.IsNullOrEmpty(user.Email) &&
                    (user.UserName == user.Email ||
                     user.UserName?.Contains("@") == true))
                {
                    string baseUsername =
                        user.Email
                            .Split('@')[0]
                            .ToLower();

                    string newUsername = baseUsername;

                    int counter = 1;

                    while (true)
                    {
                        var existingUser =
                            await userManager
                                .FindByNameAsync(newUsername);

                        if (existingUser == null ||
                            existingUser.Id == user.Id)
                        {
                            break;
                        }

                        newUsername =
                            baseUsername + counter;

                        counter++;
                    }

                    var usernameResult =
                        await userManager.SetUserNameAsync(
                            user,
                            newUsername
                        );

                    if (!usernameResult.Succeeded)
                    {
                        var errors = string.Join(
                            ", ",
                            usernameResult.Errors
                                .Select(e => e.Description)
                        );

                        throw new Exception(
                            $"Failed to update username: {errors}"
                        );
                    }
                }
            }
        }

        // =========================
        // CREATE SYSTEM USER
        // =========================
        private static async Task CreateUserWithRoleAsync(
            UserManager<ApplicationUser> userManager,
            string fullName,
            string username,
            string email,
            string password,
            string role)
        {
            var user =
                await userManager.FindByEmailAsync(email);

            // =========================
            // CREATE USER IF NOT EXISTS
            // =========================
            if (user == null)
            {
                user = new ApplicationUser
                {
                    FullName = fullName,
                    UserName = username,
                    Email = email,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var createResult =
                    await userManager.CreateAsync(
                        user,
                        password
                    );

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        createResult.Errors
                            .Select(e => e.Description)
                    );

                    throw new Exception(
                        $"Failed to create {role} account: {errors}"
                    );
                }
            }
            else
            {
                bool userUpdated = false;

                // -------------------------
                // UPDATE FULL NAME
                // -------------------------
                if (string.IsNullOrWhiteSpace(user.FullName))
                {
                    user.FullName = fullName;
                    userUpdated = true;
                }

                // -------------------------
                // UPDATE USERNAME
                // -------------------------
                if (user.UserName != username)
                {
                    user.UserName = username;
                    userUpdated = true;
                }

                // IMPORTANT:
                // Do NOT automatically reactivate an account here.
                // If an Admin intentionally deactivates a user,
                // restarting the application must not activate it again.

                // -------------------------
                // ENSURE CREATED DATE
                // -------------------------
                if (user.CreatedAt == default)
                {
                    user.CreatedAt = DateTime.Now;
                    userUpdated = true;
                }

                if (userUpdated)
                {
                    var updateResult =
                        await userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        var errors = string.Join(
                            ", ",
                            updateResult.Errors
                                .Select(e => e.Description)
                        );

                        throw new Exception(
                            $"Failed to update {role} account: {errors}"
                        );
                    }
                }
            }

            // =========================
            // ASSIGN REQUIRED ROLE
            // =========================
            if (!await userManager.IsInRoleAsync(
                user,
                role))
            {
                var roleResult =
                    await userManager.AddToRoleAsync(
                        user,
                        role
                    );

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        roleResult.Errors
                            .Select(e => e.Description)
                    );

                    throw new Exception(
                        $"Failed to assign {role} role: {errors}"
                    );
                }
            }
        }
    }
}