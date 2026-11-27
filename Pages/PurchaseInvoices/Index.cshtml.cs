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

        if (SupplierId.HasValue && SupplierId.Value > 0)
        {
            invoices = invoices.Where(i => i.SupplierId == SupplierId.Value).ToList();
        }

        if (InvoiceId.HasValue && InvoiceId.Value > 0)
        {
            invoices = invoices.Where(i => i.PurchaseInvoiceId == InvoiceId.Value).ToList();
        }

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
        if (SelectedDate.HasValue)
        {
            invoices = invoices.Where(i => i.InvoiceDate == SelectedDate.Value).ToList();
        }

        allPurchaseInvoices = invoices;
    }
}
