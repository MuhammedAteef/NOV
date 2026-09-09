using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.StockHistory;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<SystemManagementSystem.Models.StockHistory> allStockHistories = new List<SystemManagementSystem.Models.StockHistory>();

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        allStockHistories = await _unitOfWork.StockHistories
            .Query()
            .Include(s => s.Product)
            .ToListAsync();
    }
}
