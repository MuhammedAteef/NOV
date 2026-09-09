using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Customers;

public class DeleteModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public Customer Customer { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _unitOfWork.Customers.GetByIdAsync(id.Value);
        if (customer is null) return NotFound();

        Customer = customer;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            var customerToDelete = await _unitOfWork.Customers.GetByIdAsync(id);
            if (customerToDelete == null) return NotFound();

            _unitOfWork.Customers.Remove(customerToDelete);
            await _unitOfWork.SaveChangesAsync();

            TempData["SuccessMessage"] = "Customer deleted successfully.";
            return RedirectToPage("./Index");
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Cannot delete this customer because they are linked to existing sales invoices.";
            return RedirectToPage("./Index");
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "An unexpected error occurred while attempting to delete the customer.";
            return RedirectToPage("./Index");
        }
    }
}
