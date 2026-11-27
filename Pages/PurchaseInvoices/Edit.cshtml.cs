using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

public class EditModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public EditModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public PurchaseInvoice Invoice { get; set; } = default!;

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = new();

    public SelectList SupplierList { get; set; } = default!;
    public SelectList ProductList  { get; set; } = default!;

    public List<PurchaseInvoiceItem> ExistingItems { get; set; } = new();
    [BindProperty]
    public decimal changedAmount { get; set; }


    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        Invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id.Value);

        if (Invoice is null) return NotFound();

        TempData["ErrorMessage"] = "Purchase invoices are immutable. Create a new invoice for additional products or a purchase refund for returns.";
        return RedirectToPage("./Show", new { id = id.Value });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var id = Invoice?.PurchaseInvoiceId ?? 0;
        if (id <= 0 || await _unitOfWork.PurchaseInvoices.GetByIdAsync(id) is null)
        {
            return NotFound();
        }

        TempData["ErrorMessage"] = "Purchase invoices are immutable. Create a new invoice for additional products or a purchase refund for returns.";
        return RedirectToPage("./Show", new { id });
    }
}
