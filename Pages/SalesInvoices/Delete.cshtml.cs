using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.SalesInvoices;

public class DeleteModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public SalesInvoice Invoice { get; set; } = default!;
    public List<SalesInvoiceItem> LineItems { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var invoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(id.Value);
        if (invoice is null) return NotFound();

        Invoice = invoice;
        LineItems = Invoice.SalesInvoiceItems.ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(id);
        if (invoice is null) return NotFound();

        if (invoice.IsDeleted)
        {
            TempData["ErrorMessage"] = $"Invoice #{id} is already marked as deleted.";
            return RedirectToPage("./Index");
        }

        await _unitOfWork.SalesInvoices.SoftDeleteAsync(id);

        TempData["SuccessMessage"] = $"Sales Invoice #{id} has been deleted and stock has been restored.";
        return RedirectToPage("./Index");
    }
}
