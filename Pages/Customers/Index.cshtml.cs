using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.Customers;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<Customer> allCustomers = new List<Customer>();

    public Dictionary<int, decimal> CustomerTotals;

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        allCustomers = await _unitOfWork.Customers.GetAllAsync();
        CustomerTotals = await _unitOfWork.SalesInvoices.Query()
        .GroupBy(x => x.CustomerId)
        .ToDictionaryAsync(
            g => g.Key,
            g => g.Sum(x => x.TotalAmount)
        );
    }
}
