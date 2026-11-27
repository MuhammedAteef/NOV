using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.SalesInvoices;

public class ShowModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public ShowModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public SalesInvoice Invoice { get; set; } = default!;
    public List<SalesInvoiceItem> LineItems { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<SalesReturn> Returns { get; set; } = new();
    public decimal TotalPaid { get; set; }
    public decimal Remaining { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var invoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(id.Value);
        if (invoice is null)
        {
            return NotFound();
        }

        Invoice = invoice;
        LineItems = invoice.SalesInvoiceItems.ToList();
        Payments = await _unitOfWork.Payments.Query()
            .Where(payment => payment.SalesInvoiceId == id && payment.Status == "Paid")
            .OrderByDescending(payment => payment.PaymentDate)
            .ToListAsync();
        TotalPaid = Payments.Sum(payment => payment.Amount);
        Remaining = Invoice.TotalAmount - TotalPaid;

        Returns = await _unitOfWork.SalesReturns.Query()
            .Include(salesReturn => salesReturn.Items)
            .Include(salesReturn => salesReturn.Refunds)
            .Where(salesReturn => salesReturn.InvoiceId == id)
            .OrderByDescending(salesReturn => salesReturn.ReturnDate)
            .ToListAsync();

        return Page();
    }
}
