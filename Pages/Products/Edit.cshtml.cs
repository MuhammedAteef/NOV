using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Products;

public class EditModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public EditModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public Product Product { get; set; } = default!;

    public SelectList CategoryList { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var product = await _unitOfWork.Products.GetByIdAsync(id.Value);
        if (product is null) return NotFound();

        Product = product;
        var categories = await _unitOfWork.Categories.GetAllAsync();
        CategoryList = new SelectList(categories, "CategoryId", "Name", Product.CategoryId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Product.Category");

        if (!ModelState.IsValid)
        {
            var categories = await _unitOfWork.Categories.GetAllAsync();
            CategoryList = new SelectList(categories, "CategoryId", "Name", Product.CategoryId);
            return Page();
        }

        _unitOfWork.Products.Update(Product);
        await _unitOfWork.SaveChangesAsync();
        TempData["SuccessMessage"] = "Product updated successfully.";
        return RedirectToPage("./Index");
    }
}
