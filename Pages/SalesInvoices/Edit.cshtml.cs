using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.SalesInvoices;

public class EditModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceWorkflowService _invoiceWorkflow;

    public EditModel(IUnitOfWork unitOfWork, IInvoiceWorkflowService invoiceWorkflow)
    {
        _unitOfWork = unitOfWork;
        _invoiceWorkflow = invoiceWorkflow;
    }

    public SalesInvoice Invoice { get; set; } = default!;

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = new();

    [BindProperty]
    public string? ReturnReason { get; set; }

    [BindProperty]
    public string RequestKey { get; set; } = "";

    public List<SalesInvoiceItem> ExistingItems { get; set; } = new();

    public Dictionary<int, int> PreviouslyReturnedQuantities { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var invoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(id.Value);
        if (invoice is null || invoice.IsDeleted)
        {
            return NotFound();
        }

        Invoice = invoice;
        RequestKey = Guid.NewGuid().ToString("N");
        await LoadReturnDataAsync(invoice);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(id);
        if (invoice is null || invoice.IsDeleted)
        {
            return NotFound();
        }

        Invoice = invoice;
        await LoadReturnDataAsync(invoice);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var salesReturn = await _invoiceWorkflow.CreateSalesReturnAsync(
                id,
                LineItems,
                ReturnReason,
                RequestKey);

            TempData["SuccessMessage"] = $"Sales refund #{salesReturn.Id} completed in cash.";
            return RedirectToPage("./Show", new { id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task LoadReturnDataAsync(SalesInvoice invoice)
    {
        ExistingItems = invoice.SalesInvoiceItems
            .Where(item => !item.IsDeleted)
            .ToList();

        var invoiceItemIds = ExistingItems.Select(item => item.SalesItemId).ToArray();
        PreviouslyReturnedQuantities = await _unitOfWork.SalesReturnItems.Query()
            .Where(item => invoiceItemIds.Contains(item.InvoiceItemId) &&
                item.SalesReturn.Status == ReturnStatus.Completed)
            .GroupBy(item => item.InvoiceItemId)
            .Select(group => new
            {
                InvoiceItemId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToDictionaryAsync(group => group.InvoiceItemId, group => group.Quantity);
    }
}
