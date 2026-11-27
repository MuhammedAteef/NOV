using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages;

public class DashboardModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public decimal RevenueToday { get; set; }
    public int TotalProducts { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalSalesInvoices { get; set; }
    public int LowStockCount { get; set; }
    public string GreetingMessage { get; set; } = "Welcome back";

    public List<SalesInvoice> RecentSales { get; set; } = new();
    public List<Product> LowStockProducts { get; set; } = new();

    public async Task OnGetAsync()
    {
        var hour = DateTime.Now.Hour;
        if (hour < 12) GreetingMessage = "Good Morning";
        else if (hour < 18) GreetingMessage = "Good Afternoon";
        else GreetingMessage = "Good Evening";

        TotalProducts = await _unitOfWork.Products.Query().CountAsync();
        LowStockProducts = await _unitOfWork.Products.Query().Where(p => p.StockQuantity < 10).OrderBy(p => p.StockQuantity).Take(5).ToListAsync();
        LowStockCount = await _unitOfWork.Products.Query().CountAsync(p => p.StockQuantity < 10);

        TotalCustomers = await _unitOfWork.Customers.Query().CountAsync();

        TotalSalesInvoices = await _unitOfWork.SalesInvoices.Query().CountAsync(invoice => !invoice.IsDeleted);

        var today = DateTime.Today;
        var paidSalesToday = await _unitOfWork.Payments.Query()
            .Where(payment => payment.Status == "Paid" &&
                payment.SalesInvoiceId.HasValue &&
                payment.PaymentDate.Date == today &&
                payment.SalesInvoice != null &&
                !payment.SalesInvoice.IsDeleted)
            .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
        var activeSalesInvoiceIds = _unitOfWork.SalesInvoices.Query()
            .Where(invoice => !invoice.IsDeleted)
            .Select(invoice => invoice.SalesInvoiceId);
        var salesRefundsToday = await _unitOfWork.SalesReturns.Query()
            .Where(salesReturn => salesReturn.Status == ReturnStatus.Completed &&
                salesReturn.ReturnDate.Date == today &&
                activeSalesInvoiceIds.Contains(salesReturn.InvoiceId))
            .SumAsync(salesReturn => (decimal?)salesReturn.TotalAmount) ?? 0m;
        RevenueToday = paidSalesToday - salesRefundsToday;

        RecentSales = await _unitOfWork.SalesInvoices.Query()
            .Where(invoice => !invoice.IsDeleted)
            .Include(invoice => invoice.Customer)
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .Take(5)
            .ToListAsync();
    }
}
