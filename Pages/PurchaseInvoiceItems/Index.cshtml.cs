using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.PurchaseInvoiceItems;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<PurchaseInvoiceItem> allPurchaseInvoiceItems = new List<PurchaseInvoiceItem>();

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        allPurchaseInvoiceItems = await _unitOfWork.PurchaseInvoiceItems
            .Query()
            .Include(i => i.Product)
            .Include(i => i.PurchaseInvoice)
            .ToListAsync();
    }
}
