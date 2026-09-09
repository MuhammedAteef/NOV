using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.SalesInvoiceItems;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<SalesInvoiceItem> allSalesInvoiceItems = new List<SalesInvoiceItem>();

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        allSalesInvoiceItems = await _unitOfWork.SalesInvoiceItems
            .Query()
            .Include(i => i.Product)
            .Include(i => i.SalesInvoice)
            .ToListAsync();
    }
}
