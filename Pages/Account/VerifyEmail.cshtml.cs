using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.Account;

public class VerifyEmailModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IAuthService _authService;

    public VerifyEmailModel(UserManager<IdentityUser> userManager, IAuthService authService)
    {
        _userManager = userManager;
        _authService = authService;
    }

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostVerifyAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _authService.VerifyEmailAsync(Email, Input.Code);

        if (result.Succeeded)
        {
            return RedirectToPage("/Account/Login");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
    }

    public async Task<IActionResult> OnPostResendCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ModelState.AddModelError(string.Empty, "Email is required.");
            return Page();
        }

        var result = await _authService.ResendVerificationCode(Email);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        return Page();
    }
}