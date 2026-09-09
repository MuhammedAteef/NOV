using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    public List<PurchaseInvoice> allPurchaseInvoices = new List<PurchaseInvoice>();

    public SelectList SupplierList { get; set; } = default!;

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SupplierId { get; set; }

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
        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        SupplierList = new SelectList(suppliers, "SupplierId", "Name", SupplierId);

        var invoices = await _unitOfWork.PurchaseInvoices.GetAllWithSupplierAsync();

        // 1. Filter by Supplier ID
        if (SupplierId.HasValue && SupplierId.Value > 0)
        {
            invoices = invoices.Where(i => i.SupplierId == SupplierId.Value).ToList();
        }

        // 2. Filter by Invoice ID
        if (InvoiceId.HasValue && InvoiceId.Value > 0)
        {
            invoices = invoices.Where(i => i.PurchaseInvoiceId == InvoiceId.Value).ToList();
        }

        // 3. Search Term (Invoice # or Supplier Name)
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            string term = SearchTerm.Trim();
            if (int.TryParse(term, out int parsedId))
            {
                invoices = invoices.Where(i => i.PurchaseInvoiceId == parsedId ||
                                              (i.Supplier != null && i.Supplier.Name.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            else
            {
                invoices = invoices.Where(i => i.Supplier != null && i.Supplier.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        // 4. Filter by Status (Completed, UnCompleted, Deleted)
        if (!string.IsNullOrWhiteSpace(Status))
        {
            if (Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                invoices = invoices.Where(i => i.RemainingAmount == 0 && !i.IsDeleted).ToList();
            }
            else if (Status.Equals("UnCompleted", StringComparison.OrdinalIgnoreCase) || Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                invoices = invoices.Where(i => i.RemainingAmount > 0 && !i.IsDeleted).ToList();
            }
            else if (Status.Equals("Deleted", StringComparison.OrdinalIgnoreCase))
            {
                invoices = invoices.Where(i => i.IsDeleted).ToList();
            }
        }
        // 5. Filter by Selected Date
        if (SelectedDate.HasValue)
        {
            invoices = invoices.Where(i => i.InvoiceDate == SelectedDate.Value).ToList();
        }

        allPurchaseInvoices = invoices;
    }
}
