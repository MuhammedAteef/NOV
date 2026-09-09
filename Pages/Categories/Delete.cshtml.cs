using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Categories;

public class DeleteModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public Category Category { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var category = await _unitOfWork.Categories.GetByIdAsync(id.Value);
        if (category is null) return NotFound();

        Category = category;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            var categoryToDelete = await _unitOfWork.Categories.GetByIdAsync(id);
            if (categoryToDelete == null) return NotFound();

            _unitOfWork.Categories.Remove(categoryToDelete);
            await _unitOfWork.SaveChangesAsync();

            TempData["SuccessMessage"] = "Category deleted successfully.";
            return RedirectToPage("./Index");
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Cannot delete this category because it is linked to existing products.";
            return RedirectToPage("./Index");
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "An unexpected error occurred while attempting to delete the category.";
            return RedirectToPage("./Index");
        }
    }
}
