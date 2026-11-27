using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.SalesInvoices;

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

    public SalesInvoice Invoice { get; set; } = default!;
    public List<SalesInvoiceItem> LineItems { get; set; } = new();

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

        if (invoice.IsDeleted)
        {
            TempData["ErrorMessage"] = "This sales invoice has already been deleted.";
            return RedirectToPage("./Index");
        }

        Invoice = invoice;
        LineItems = invoice.SalesInvoiceItems.ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            await _invoiceWorkflow.SoftDeleteSalesInvoiceAsync(id);
            TempData["SuccessMessage"] = $"Sales invoice #{id} was deleted and its remaining stock was restored.";
            return RedirectToPage("./Index");
        }
        catch (InvalidOperationException exception)
        {
            var invoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(id);
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
            LineItems = invoice.SalesInvoiceItems.ToList();
            return Page();
        }
    }
}
