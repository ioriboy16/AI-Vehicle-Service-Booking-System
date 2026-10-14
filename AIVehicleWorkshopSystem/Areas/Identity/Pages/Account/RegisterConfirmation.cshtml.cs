#nullable disable

using System.Threading.Tasks;
using AIVehicleWorkshopSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIVehicleWorkshopSystem.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public RegisterConfirmationModel(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string Email { get; set; }

        public bool DisplayConfirmAccountLink { get; set; } = false;

        public string EmailConfirmationUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(
            string email,
            string returnUrl = null)
        {
            if (email == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return NotFound();
            }

            Email = email;

            // Real email confirmation is sent during registration.
            // Never display a direct confirmation link on this page.
            DisplayConfirmAccountLink = false;
            EmailConfirmationUrl = null;

            return Page();
        }
    }
}