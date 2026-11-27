using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

public class RefundModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceWorkflowService _invoiceWorkflow;

    public RefundModel(IUnitOfWork unitOfWork, IInvoiceWorkflowService invoiceWorkflow)
    {
        _unitOfWork = unitOfWork;
        _invoiceWorkflow = invoiceWorkflow;
    }

    public PurchaseInvoice Invoice { get; set; } = default!;
    public List<PurchaseInvoiceItem> ExistingItems { get; set; } = new();
    public Dictionary<int, int> PreviouslyRefundedQuantities { get; set; } = new();

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = new();

    [BindProperty]
    public string? Notes { get; set; }

    [BindProperty]
    public string RequestKey { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id.Value);
        if (invoice is null || invoice.IsDeleted)
        {
            return NotFound();
        }

        Invoice = invoice;
        RequestKey = Guid.NewGuid().ToString("N");
        await LoadRefundDataAsync(invoice);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id);
        if (invoice is null || invoice.IsDeleted)
        {
            return NotFound();
        }

        Invoice = invoice;
        await LoadRefundDataAsync(invoice);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var refund = await _invoiceWorkflow.CreatePurchaseRefundAsync(
                id,
                LineItems,
                Notes,
                RequestKey);

            TempData["SuccessMessage"] = $"Purchase refund #{refund.Id} recorded.";
            return RedirectToPage("./Show", new { id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task LoadRefundDataAsync(PurchaseInvoice invoice)
    {
        ExistingItems = invoice.PurchaseInvoiceItems
            .Where(item => !item.IsDeleted)
            .ToList();
        var itemIds = ExistingItems.Select(item => item.PurchaseItemId).ToArray();
        PreviouslyRefundedQuantities = await _unitOfWork.PurchaseRefundItems.Query()
            .Where(item => itemIds.Contains(item.PurchaseInvoiceItemId) &&
                item.PurchaseRefund.Status == RefundStatus.Completed)
            .GroupBy(item => item.PurchaseInvoiceItemId)
            .Select(group => new
            {
                PurchaseInvoiceItemId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToDictionaryAsync(group => group.PurchaseInvoiceItemId, group => group.Quantity);
    }
}
