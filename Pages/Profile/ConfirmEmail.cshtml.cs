using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SystemManagementSystem.Pages.Profile;

[AllowAnonymous]
public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;

    public ConfirmEmailModel(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public bool Succeeded { get; private set; }
    public string StatusMessage { get; private set; } = "The confirmation link is invalid or has expired.";

    public async Task OnGetAsync(string? userId, string? email, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        var result = await _userManager.ChangeEmailAsync(user, email, code);
        if (!result.Succeeded)
        {
            return;
        }

        Succeeded = true;
        StatusMessage = "Your email address has been updated successfully.";
    }
}
