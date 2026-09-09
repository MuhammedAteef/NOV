using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.SalesInvoices;

public class ShowModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _config;

    public ShowModel(IUnitOfWork unitOfWork, IPaymentService paymentService, IConfiguration config)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _config = config;
    }

    public SalesInvoice Invoice { get; set; } = default!;
    public List<SalesInvoiceItem> LineItems { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public decimal TotalPaid { get; set; }
    public decimal Remaining { get; set; }
    public string StripePublishableKey { get; set; } = "";

    [BindProperty]
    public decimal PayAmount { get; set; }

    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash";

    [BindProperty]
    public string? StripePaymentIntentId { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        StripePublishableKey = _config["Stripe:PublishableKey"] ?? "";
        Invoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(id.Value);

        if (Invoice is null) return NotFound();

        LineItems = Invoice.SalesInvoiceItems.ToList();

        Payments = await _unitOfWork.Payments.FindAsync(p => p.SalesInvoiceId == id && p.Status == "Paid");
        Payments = Payments.OrderByDescending(p => p.PaymentDate).ToList();

        TotalPaid = Payments.Sum(p => p.Amount);
        Remaining = Invoice.TotalAmount - TotalPaid;

        return Page();
    }

    // ── AJAX Handler for creating Stripe Payment Intent in Pay Remaining Modal ──
    public async Task<IActionResult> OnPostCreateIntentAsync([FromForm] decimal amount)
    {
        try
        {
            if (amount <= 0) return new JsonResult(new { error = "Amount must be greater than zero." }) { StatusCode = 400 };
            var (clientSecret, intentId) = await _paymentService.CreateStripePaymentIntentAsync(amount);
            return new JsonResult(new { clientSecret, intentId });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 400 };
        }
    }

    // ── Handler for Pay Remaining Submit ──
    public async Task<IActionResult> OnPostPayRemainingAsync(int id)
    {
        StripePublishableKey = _config["Stripe:PublishableKey"] ?? "";
        Invoice = await _unitOfWork.SalesInvoices.GetByIdAsync(id);
        if (Invoice == null || Invoice.IsDeleted) return NotFound();

        var existingPayments = await _unitOfWork.Payments.FindAsync(p => p.SalesInvoiceId == id && p.Status == "Paid");
        decimal currentPaid = existingPayments.Sum(p => p.Amount);
        decimal currentRemaining = Invoice.TotalAmount - currentPaid;

        if (PayAmount <= 0)
        {
            TempData["ErrorMessage"] = "Payment amount must be greater than zero.";
            return RedirectToPage(new { id });
        }

        if (PayAmount > currentRemaining)
        {
            TempData["ErrorMessage"] = $"Payment amount (${PayAmount:N2}) cannot exceed the remaining balance of ${currentRemaining:N2}.";
            return RedirectToPage(new { id });
        }

        if (PaymentMethod == "Card")
        {
            if (string.IsNullOrEmpty(StripePaymentIntentId))
            {
                TempData["ErrorMessage"] = "Stripe Card Payment transaction incomplete.";
                return RedirectToPage(new { id });
            }

            var paymentStatus = await _paymentService.PaymentStatusAsync(StripePaymentIntentId);
            if (paymentStatus != "succeeded")
            {
                TempData["ErrorMessage"] = "Card payment was not completed successfully.";
                return RedirectToPage(new { id });
            }
        }

        // Record the payment entry in Payment table via PaymentService
        await _paymentService.ProcessInvoicePaymentAsync(
            salesInvoiceId: Invoice.SalesInvoiceId,
            purchaseInvoiceId: null,
            amount: PayAmount,
            paymentMethod: PaymentMethod,
            stripePaymentIntentId: StripePaymentIntentId
        );

        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Payment of ${PayAmount:N2} processed successfully for Sales Invoice #{Invoice.SalesInvoiceId}.";
        return RedirectToPage(new { id });
    }
}
