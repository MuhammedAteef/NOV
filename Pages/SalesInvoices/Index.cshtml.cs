using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.SalesInvoices;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<SalesInvoice> allSalesInvoices = new List<SalesInvoice>();

    public SelectList CustomerList { get; set; } = default!;

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CustomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? InvoiceId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? SelectedDate { get; set; }

    public IndexModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task OnGetAsync()
    {
        var customers = await _unitOfWork.Customers.GetAllAsync();
        CustomerList = new SelectList(customers, "CustomerId", "Name", CustomerId);

        var invoices = await _unitOfWork.SalesInvoices.GetAllWithCustomerAsync();

        // 1. Filter by Customer ID
        if (CustomerId.HasValue && CustomerId.Value > 0)
        {
            invoices = invoices.Where(i => i.CustomerId == CustomerId.Value).ToList();
        }

        // 2. Filter by Invoice ID
        if (InvoiceId.HasValue && InvoiceId.Value > 0)
        {
            invoices = invoices.Where(i => i.SalesInvoiceId == InvoiceId.Value).ToList();
        }

        // 3. Search Term (Invoice # or Customer Name)
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            string term = SearchTerm.Trim();
            if (int.TryParse(term, out int parsedId))
            {
                invoices = invoices.Where(i => i.SalesInvoiceId == parsedId ||
                                              (i.Customer != null && i.Customer.Name.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            else
            {
                invoices = invoices.Where(i => i.Customer != null && i.Customer.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        // 4. Filter by Status
        if (!string.IsNullOrWhiteSpace(Status))
        {
            if (Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                invoices = invoices.Where(i => !i.IsDeleted).ToList();
            }
            else if (Status.Equals("Deleted", StringComparison.OrdinalIgnoreCase))
            {
                invoices = invoices.Where(i => i.IsDeleted).ToList();
            }
        }

        // 5. Filter by Selected Date
        if (SelectedDate.HasValue)
        {
            invoices = invoices.Where(i => i.InvoiceDate.Date == SelectedDate.Value.Date).ToList();
        }

        allSalesInvoices = invoices;
    }
}
