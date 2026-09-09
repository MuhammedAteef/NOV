using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly IAuthService _authService;

    public RegisterModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public RegisterModelDTO Input { get; set; } = new();

    public string? ReturnUrl { get; set; }


    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("/VerifyEmail");

        if (ModelState.IsValid)
        {
            var result = await _authService.RegisterUserAsync(Input.Email, Input.Password, Input.Role);

            if (result.Succeeded)
            {
                return RedirectToPage("/Account/VerifyEmail", new { email = Input.Email });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return Page();
    }
}
