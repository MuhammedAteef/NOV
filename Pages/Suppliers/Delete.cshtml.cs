using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Suppliers;

public class DeleteModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public Supplier Supplier { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(id.Value);
        if (supplier is null) return NotFound();

        Supplier = supplier;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            var supplierToDelete = await _unitOfWork.Suppliers.GetByIdAsync(id);
            if (supplierToDelete == null) return NotFound();

            _unitOfWork.Suppliers.Remove(supplierToDelete);
            await _unitOfWork.SaveChangesAsync();

            TempData["SuccessMessage"] = "Supplier deleted successfully.";
            return RedirectToPage("./Index");
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Cannot delete this supplier because they are linked to existing records.";
            return RedirectToPage("./Index");
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "An unexpected error occurred while attempting to delete the supplier.";
            return RedirectToPage("./Index");
        }
    }
}
