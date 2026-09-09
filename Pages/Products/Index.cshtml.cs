using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Products;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<Product> allProducts = new List<Product>();

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        allProducts = await _unitOfWork.Products.Query().Include(p => p.Category).ToListAsync();
    }
}
