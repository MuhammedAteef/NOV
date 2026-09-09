using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

public class DeleteModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public PurchaseInvoice Invoice { get; set; } = default!;
    public List<PurchaseInvoiceItem> LineItems { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id.Value);
        if (invoice is null) return NotFound();

        Invoice = invoice;
        LineItems = Invoice.PurchaseInvoiceItems.ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id);
        if (invoice is null) return NotFound();

        if (invoice.IsDeleted)
        {
            TempData["ErrorMessage"] = $"Invoice #{id} is already marked as deleted.";
            return RedirectToPage("./Index");
        }

        await _unitOfWork.PurchaseInvoices.SoftDeleteAsync(id);

        TempData["SuccessMessage"] = $"Purchase Invoice #{id} has been deleted and stock has been reversed.";
        return RedirectToPage("./Index");
    }
}
