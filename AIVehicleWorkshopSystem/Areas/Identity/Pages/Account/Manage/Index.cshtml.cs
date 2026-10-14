#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AIVehicleWorkshopSystem.Models;
using AIVehicleWorkshopSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIVehicleWorkshopSystem.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly DataEncryptionService _encryptionService;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            DataEncryptionService encryptionService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _encryptionService = encryptionService;
        }

        public string Username { get; set; }

        public string Email { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(100)]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Phone]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            Username =
                await _userManager.GetUserNameAsync(user);

            Email =
                await _userManager.GetEmailAsync(user);

            string decryptedPhone = string.Empty;

            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                decryptedPhone =
                    _encryptionService.Decrypt(
                        user.PhoneNumber
                    );
            }

            Input = new InputModel
            {
                FullName = user.FullName,

                PhoneNumber =
                    decryptedPhone
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound(
                    $"Unable to load user with ID " +
                    $"'{_userManager.GetUserId(User)}'."
                );
            }

            await LoadAsync(user);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound(
                    $"Unable to load user with ID " +
                    $"'{_userManager.GetUserId(User)}'."
                );
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);

                return Page();
            }


            // ============================================
            // UPDATE FULL NAME
            // ============================================

            user.FullName =
                Input.FullName.Trim();


            // ============================================
            // UPDATE ENCRYPTED PHONE NUMBER
            // ============================================

            string currentPhone = string.Empty;

            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                currentPhone =
                    _encryptionService.Decrypt(
                        user.PhoneNumber
                    );
            }


            string newPhone =
                Input.PhoneNumber?.Trim()
                ?? string.Empty;


            if (currentPhone != newPhone)
            {
                user.PhoneNumber =
                    string.IsNullOrWhiteSpace(newPhone)
                        ? null
                        : _encryptionService.Encrypt(
                            newPhone
                        );

                // Phone has changed.
                user.PhoneNumberConfirmed = false;
            }


            // ============================================
            // SAVE
            // ============================================

            var result =
                await _userManager.UpdateAsync(user);


            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description
                    );
                }

                await LoadAsync(user);

                return Page();
            }


            await _signInManager.RefreshSignInAsync(
                user
            );


            StatusMessage =
                "Your profile has been updated successfully.";


            return RedirectToPage();
        }
    }
}