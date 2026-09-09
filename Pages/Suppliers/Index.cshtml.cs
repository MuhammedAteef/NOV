using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Suppliers;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<Supplier> allSuppliers = new List<Supplier>();

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        allSuppliers = await _unitOfWork.Suppliers.GetAllAsync();
    }
}
