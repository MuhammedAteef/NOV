using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Stripe;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

public class ShowModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IInvoiceWorkflowService _invoiceWorkflow;
    private readonly IConfiguration _config;
    private readonly ILogger<ShowModel> _logger;

    public ShowModel(
        IUnitOfWork unitOfWork,
        IPaymentService paymentService,
        IInvoiceWorkflowService invoiceWorkflow,
        IConfiguration config,
        ILogger<ShowModel> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _invoiceWorkflow = invoiceWorkflow;
        _config = config;
        _logger = logger;
    }

    public PurchaseInvoice Invoice { get; set; } = default!;
    public List<PurchaseInvoiceItem> LineItems { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<PurchaseRefund> Refunds { get; set; } = new();
    public decimal TotalPaid { get; set; }
    public decimal Remaining { get; set; }
    public string StripePublishableKey { get; set; } = "";

    [BindProperty]
    public decimal PayAmount { get; set; }

    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash";

    [BindProperty]
    public string? StripePaymentIntentId { get; set; }

    [BindProperty]
    public string PaymentRequestKey { get; set; } = "";

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

        Invoice = invoice;
        LineItems = invoice.PurchaseInvoiceItems.ToList();
        Payments = await _unitOfWork.Payments.Query()
            .Where(payment => payment.PurchaseInvoiceId == id && payment.Status == "Paid")
            .OrderByDescending(payment => payment.PaymentDate)
            .ToListAsync();
        TotalPaid = Payments.Sum(payment => payment.Amount);
        Remaining = Math.Max(0, Invoice.TotalAmount - TotalPaid);
        StripePublishableKey = _config["Stripe:PublishableKey"] ?? "";
        PaymentRequestKey = Guid.NewGuid().ToString("N");

        Refunds = await _unitOfWork.PurchaseRefunds.Query()
            .Include(refund => refund.Items)
            .Where(refund => refund.PurchaseInvoiceId == id)
            .OrderByDescending(refund => refund.RefundDate)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostCreateIntentAsync([FromForm] decimal amount, [FromForm] int id)
    {
        var invoice = await _unitOfWork.PurchaseInvoices.GetByIdAsync(id);
        if (invoice is null || invoice.IsDeleted)
        {
            return new JsonResult(new { error = "Purchase invoice was not found." })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        var payments = await _unitOfWork.Payments.FindAsync(
            payment => payment.PurchaseInvoiceId == id && payment.Status == "Paid");
        var remaining = invoice.TotalAmount - payments.Sum(payment => payment.Amount);
        if (amount <= 0 || amount > remaining || decimal.Round(amount, 2) != amount)
        {
            return new JsonResult(new { error = "Payment must be positive and cannot exceed the remaining invoice balance." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        try
        {
            var (clientSecret, intentId) = await _paymentService.CreateStripePaymentIntentAsync(amount);
            return new JsonResult(new { clientSecret, intentId });
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Stripe could not authorize a purchase invoice payment.");
            return new JsonResult(new { error = "Card authorization failed. Please try again." })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }
    }

    public async Task<IActionResult> OnPostPayRemainingAsync(int id)
    {
        try
        {
            var payment = await _invoiceWorkflow.AddPurchasePaymentAsync(
                id,
                PayAmount,
                PaymentMethod,
                PaymentRequestKey,
                StripePaymentIntentId);

            TempData["SuccessMessage"] = $"Payment of ${payment.Amount:N2} processed for Purchase Invoice #{id}.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Stripe payment processing failed for purchase invoice {InvoiceId}.", id);
            TempData["ErrorMessage"] = "Card payment could not be completed. The purchase invoice was not updated.";
        }

        return RedirectToPage(new { id });
    }
}
