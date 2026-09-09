using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Products;

public class DeleteModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public Product Product { get; set; } = default!;

    public async Task<IActionResult> OnGet(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        Product = await _unitOfWork.Products
            .Query()
            .Include(p => p.Category)
            .SingleOrDefaultAsync(p => p.ProductId == id);

        if (Product is null)
        {
            return NotFound();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            var productToDelete = await _unitOfWork.Products.GetByIdAsync(id);

            if (productToDelete == null)
            {
                return NotFound();
            }

            _unitOfWork.Products.Remove(productToDelete);
            await _unitOfWork.SaveChangesAsync();

            TempData["SuccessMessage"] = "Product deleted successfully.";
            return RedirectToPage("./Index");
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Cannot delete this product because it is linked to existing invoice items or stock records.";
            return RedirectToPage("./Index");
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "An unexpected error occurred while attempting to delete the product.";
            return RedirectToPage("./Index");
        }
    }
}
