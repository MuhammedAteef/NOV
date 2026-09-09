using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Customers;

public class CreateModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public Customer Customer { get; set; } = default!;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _unitOfWork.Customers.AddAsync(Customer);
        await _unitOfWork.SaveChangesAsync();
        TempData["SuccessMessage"] = "Customer created successfully.";

        return RedirectToPage("./Index");
    }
}
