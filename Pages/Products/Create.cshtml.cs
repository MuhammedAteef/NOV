using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Products;

public class CreateModel : PageModel
{
    private IUnitOfWork _unitOfWork;

    public CreateModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public Product Product { get; set; } = default!;

    public SelectList CategoryList { get; set; } = default!;

    public async Task OnGet()
    {
        CategoryList = new SelectList(await _unitOfWork.Categories.GetAllAsync(), "CategoryId", "Name");
    }

    public async Task<IActionResult> OnPost()
    {
        ModelState.Remove("Product.Category");

        if (!ModelState.IsValid)
        {
            CategoryList = new SelectList(await _unitOfWork.Categories.GetAllAsync(), "CategoryId", "Name");
            return Page();
        }

        await _unitOfWork.Products.AddAsync(Product);
        await _unitOfWork.SaveChangesAsync();
        TempData["SuccessMessage"] = "Product created successfully.";
        return RedirectToPage("./Index");
    }
}
