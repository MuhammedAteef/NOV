using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

[Authorize(Policy = "AdminOnly")]
public class DeleteModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceWorkflowService _invoiceWorkflow;

    public DeleteModel(IUnitOfWork unitOfWork, IInvoiceWorkflowService invoiceWorkflow)
    {
        _unitOfWork = unitOfWork;
        _invoiceWorkflow = invoiceWorkflow;
    }

    public PurchaseInvoice Invoice { get; set; } = default!;
    public List<PurchaseInvoiceItem> LineItems { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id.Value);
        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.IsDeleted)
        {
            TempData["ErrorMessage"] = "This purchase invoice has already been deleted.";
            return RedirectToPage("./Index");
        }

        Invoice = invoice;
        LineItems = invoice.PurchaseInvoiceItems.ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            await _invoiceWorkflow.SoftDeletePurchaseInvoiceAsync(id);
            TempData["SuccessMessage"] = $"Purchase invoice #{id} was deleted and its remaining stock was reversed.";
            return RedirectToPage("./Index");
        }
        catch (InvalidOperationException exception)
        {
            var invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id);
            if (invoice is null)
            {
                return NotFound();
            }

            if (invoice.IsDeleted)
            {
                TempData["ErrorMessage"] = exception.Message;
                return RedirectToPage("./Index");
            }

            ModelState.AddModelError(string.Empty, exception.Message);
            Invoice = invoice;
            LineItems = invoice.PurchaseInvoiceItems.ToList();
            return Page();
        }
    }
}
