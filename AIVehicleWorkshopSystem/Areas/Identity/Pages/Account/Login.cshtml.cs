// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AIVehicleWorkshopSystem.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            ILogger<LoginModel> logger,
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }


        // =====================================================
        // GET LOGIN PAGE
        // =====================================================
        public async Task OnGetAsync(
            string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(
                    string.Empty,
                    ErrorMessage
                );
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(
                IdentityConstants.ExternalScheme
            );

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                    .ToList();

            ReturnUrl = returnUrl;
        }


        // =====================================================
        // LOGIN
        // =====================================================
        public async Task<IActionResult> OnPostAsync(
            string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                    .ToList();

            if (!ModelState.IsValid)
            {
                return Page();
            }


            // =================================================
            // FIND USER BY EMAIL
            // =================================================
            var user =
                await _userManager.FindByEmailAsync(
                    Input.Email
                );

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid email or password."
                );

                return Page();
            }


            // =================================================
            // CHECK ACCOUNT STATUS
            // =================================================
            if (!user.IsActive)
            {
                _logger.LogWarning(
                    "Login attempt for inactive account: {UserId}",
                    user.Id
                );

                ModelState.AddModelError(
                    string.Empty,
                    "This account has been deactivated. Please contact the administrator."
                );

                return Page();
            }


            // =================================================
            // LOGIN USING USERNAME
            // =================================================
            var result =
                await _signInManager.PasswordSignInAsync(
                    user.UserName!,
                    Input.Password,
                    Input.RememberMe,
                    lockoutOnFailure: false
                );


            // =================================================
            // LOGIN SUCCESS
            // =================================================
            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "User logged in: {UserId}",
                    user.Id
                );


                // =============================================
                // ADMIN REDIRECT
                // =============================================
                if (await _userManager.IsInRoleAsync(
                    user,
                    "Admin"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "Admin"
                    );
                }


                // =============================================
                // MECHANIC REDIRECT
                // =============================================
                if (await _userManager.IsInRoleAsync(
                    user,
                    "Mechanic"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "Mechanic"
                    );
                }


                // =============================================
                // CUSTOMER REDIRECT
                // =============================================
                if (await _userManager.IsInRoleAsync(
                    user,
                    "Customer"))
                {
                    return RedirectToAction(
                        "Dashboard",
                        "Customer"
                    );
                }


                // =============================================
                // USER HAS NO VALID ROLE
                // =============================================
                await _signInManager.SignOutAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "Your account does not have a valid system role."
                );

                return Page();
            }


            // =================================================
            // TWO-FACTOR AUTHENTICATION
            // =================================================
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage(
                    "./LoginWith2fa",
                    new
                    {
                        ReturnUrl = returnUrl,
                        RememberMe = Input.RememberMe
                    }
                );
            }


            // =================================================
            // ACCOUNT LOCKED
            // =================================================
            if (result.IsLockedOut)
            {
                _logger.LogWarning(
                    "User account locked out: {UserId}",
                    user.Id
                );

                return RedirectToPage(
                    "./Lockout"
                );
            }


            // =================================================
            // INVALID PASSWORD
            // =================================================
            ModelState.AddModelError(
                string.Empty,
                "Invalid email or password."
            );

            return Page();
        }
    }
}