// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace AIVehicleWorkshopSystem.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(
                100,
                MinimumLength = 2,
                ErrorMessage = "Full name must be between 2 and 100 characters.")]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required]
            [StringLength(
                30,
                MinimumLength = 3,
                ErrorMessage = "Username must be between 3 and 30 characters.")]
            [RegularExpression(
                @"^[a-zA-Z0-9._]+$",
                ErrorMessage = "Username can only contain letters, numbers, dots and underscores.")]
            [Display(Name = "Username")]
            public string Username { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(
                100,
                ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.",
                MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare(
                "Password",
                ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                    .ToList();
        }

        public async Task<IActionResult> OnPostAsync(
            string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                    .ToList();

            if (ModelState.IsValid)
            {
                // Check duplicate username
                var existingUsername =
                    await _userManager.FindByNameAsync(
                        Input.Username.Trim()
                    );

                if (existingUsername != null)
                {
                    ModelState.AddModelError(
                        "Input.Username",
                        "This username is already taken."
                    );

                    return Page();
                }

                // Check duplicate email
                var existingEmail =
                    await _userManager.FindByEmailAsync(
                        Input.Email.Trim()
                    );

                if (existingEmail != null)
                {
                    ModelState.AddModelError(
                        "Input.Email",
                        "An account with this email already exists."
                    );

                    return Page();
                }

                var user = CreateUser();

                // Custom user information
                user.FullName = Input.FullName.Trim();
                user.IsActive = true;
                user.CreatedAt = DateTime.Now;

                // Username is separate from email
                await _userStore.SetUserNameAsync(
                    user,
                    Input.Username.Trim(),
                    CancellationToken.None);

                await _emailStore.SetEmailAsync(
                    user,
                    Input.Email.Trim(),
                    CancellationToken.None);

                var result =
                    await _userManager.CreateAsync(
                        user,
                        Input.Password
                    );

                if (result.Succeeded)
                {
                    _logger.LogInformation(
                        "User created a new account with password."
                    );

                    // Automatically assign Customer role
                    var roleResult =
                        await _userManager.AddToRoleAsync(
                            user,
                            "Customer"
                        );

                    if (!roleResult.Succeeded)
                    {
                        await _userManager.DeleteAsync(user);

                        foreach (var error in roleResult.Errors)
                        {
                            ModelState.AddModelError(
                                string.Empty,
                                error.Description
                            );
                        }

                        return Page();
                    }

                    var userId =
                        await _userManager.GetUserIdAsync(user);

                    var code =
                        await _userManager
                            .GenerateEmailConfirmationTokenAsync(user);

                    code = WebEncoders.Base64UrlEncode(
                        Encoding.UTF8.GetBytes(code)
                    );

                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new
                        {
                            area = "Identity",
                            userId = userId,
                            code = code,
                            returnUrl = returnUrl
                        },
                        protocol: Request.Scheme
                    );

                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>."
                    );

                    if (_userManager.Options
                        .SignIn
                        .RequireConfirmedAccount)
                    {
                        return RedirectToPage(
                            "RegisterConfirmation",
                            new
                            {
                                email = Input.Email,
                                returnUrl = returnUrl
                            }
                        );
                    }
                    else
                    {
                        TempData["RegistrationSuccess"] =
                            "Your account has been created successfully. Please log in to continue.";

                        return RedirectToPage("/Account/Login", new
                        {
                            area = "Identity"
                        });
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description
                    );
                }
            }

            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator
                    .CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Can't create an instance of '{nameof(ApplicationUser)}'."
                );
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException(
                    "The default UI requires a user store with email support."
                );
            }

            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}