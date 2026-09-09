using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SystemManagementSystem.Pages;

public class IndexModel : PageModel
{
    // If the user is already authenticated, redirect them straight to the dashboard.
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Dashboard");
        }

        return Page();
    }
}
