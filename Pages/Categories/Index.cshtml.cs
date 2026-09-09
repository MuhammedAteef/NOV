using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Categories;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<Category> allCategories = new List<Category>();

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        allCategories = await _unitOfWork.Categories.GetAllAsync();
    }
}
