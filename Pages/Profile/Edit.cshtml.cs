using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.Profile;

public class EditModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailService _emailService;

    public EditModel(UserManager<IdentityUser> userManager, IEmailService emailService)
    {
        _userManager = userManager;
        _emailService = emailService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(256)]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        Input.UserName = user.UserName ?? string.Empty;
        Input.Email = user.Email ?? string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var userName = Input.UserName.Trim();
        var email = Input.Email.Trim();
        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);

        var existingUserName = await _userManager.FindByNameAsync(userName);
        if (existingUserName is not null && existingUserName.Id != user.Id)
        {
            ModelState.AddModelError("Input.UserName", "That username is already in use.");
        }

        if (emailChanged)
        {
            var existingEmail = await _userManager.FindByEmailAsync(email);
            if (existingEmail is not null && existingEmail.Id != user.Id)
            {
                ModelState.AddModelError("Input.Email", "That email address is already in use.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        string? confirmationUrl = null;
        if (emailChanged)
        {
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, email);
            confirmationUrl = Url.Page(
                "/Profile/ConfirmEmail",
                pageHandler: null,
                values: new { userId = user.Id, email, code = token },
                protocol: Request.Scheme);

            if (confirmationUrl is null)
            {
                ModelState.AddModelError(string.Empty, "Could not create an email confirmation link.");
                return Page();
            }

            var encodedConfirmationUrl = HtmlEncoder.Default.Encode(confirmationUrl);
            await _emailService.SendEmailAsync(
                email,
                "NOVERP - Confirm Your New Email",
                $"<p>Use the link below to confirm your new NOVERP email address:</p><p><a href=\"{encodedConfirmationUrl}\">Confirm email address</a></p>");
        }

        if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
        {
            var userNameResult = await _userManager.SetUserNameAsync(user, userName);
            if (!userNameResult.Succeeded)
            {
                foreach (var error in userNameResult.Errors)
                {
                    ModelState.AddModelError("Input.UserName", error.Description);
                }

                if (emailChanged)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "A confirmation email was sent, but the username could not be updated.");
                }

                return Page();
            }
        }

        TempData["StatusMessage"] = emailChanged
            ? $"Username updated. Check {email} for a confirmation link; your current email stays active until confirmation."
            : "Your profile was updated.";

        return RedirectToPage("/Profile/Index");
    }
}
