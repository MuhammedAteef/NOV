using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Customers;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<Customer> allCustomers = new List<Customer>();

    public Dictionary<int, decimal> CustomerTotals { get; set; } = new();

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        allCustomers = await _unitOfWork.Customers.GetAllAsync();
        var invoices = await _unitOfWork.SalesInvoices.Query()
            .Where(invoice => !invoice.IsDeleted)
            .Select(invoice => new { invoice.SalesInvoiceId, invoice.CustomerId, invoice.TotalAmount })
            .ToListAsync();
        var refundsByInvoice = await _unitOfWork.SalesReturns.Query()
            .Where(salesReturn => salesReturn.Status == ReturnStatus.Completed)
            .GroupBy(salesReturn => salesReturn.InvoiceId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(salesReturn => salesReturn.TotalAmount));

        CustomerTotals = invoices
            .GroupBy(invoice => invoice.CustomerId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(invoice =>
                    invoice.TotalAmount - refundsByInvoice.GetValueOrDefault(invoice.SalesInvoiceId)));
    }
}
