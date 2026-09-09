using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

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

    public PurchaseInvoice Invoice { get; set; } = default!;
    public List<PurchaseInvoiceItem> LineItems { get; set; } = new();
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
        Invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id.Value);

        if (Invoice is null) return NotFound();

        LineItems = Invoice.PurchaseInvoiceItems.ToList();

        Payments = await _unitOfWork.Payments.FindAsync(p => p.PurchaseInvoiceId == id);
        Payments = Payments.OrderByDescending(p => p.PaymentDate).ToList();

        // Calculate paid and remaining based on payments and stored columns
        TotalPaid = Payments.Sum(p => p.Amount);
        //Remaining = Invoice.TotalAmount - TotalPaid;
        Remaining = Invoice.RemainingAmount;


        // Ensure database entity fields stay in sync
        if (Invoice.PaidAmount != TotalPaid || Invoice.RemainingAmount != Remaining)
        {
            Invoice.PaidAmount = TotalPaid;
            Invoice.RemainingAmount = Remaining < 0 ? 0 : Remaining;
            _unitOfWork.PurchaseInvoices.Update(Invoice);
            await _unitOfWork.SaveChangesAsync();
        }

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
        Invoice = await _unitOfWork.PurchaseInvoices.GetByIdAsync(id);
        if (Invoice == null || Invoice.IsDeleted) return NotFound();

        var existingPayments = await _unitOfWork.Payments.FindAsync(p => p.PurchaseInvoiceId == id);
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

        // 1. Record the payment entry in Payment table via PaymentService
        await _paymentService.ProcessInvoicePaymentAsync(
            salesInvoiceId: null,
            purchaseInvoiceId: Invoice.PurchaseInvoiceId,
            amount: PayAmount,
            paymentMethod: PaymentMethod,
            stripePaymentIntentId: StripePaymentIntentId
        );

        // 2. Update Invoice PaidAmount & RemainingAmount
        Invoice.PaidAmount = currentPaid + PayAmount;
        Invoice.RemainingAmount = Invoice.TotalAmount - Invoice.PaidAmount;
        if (Invoice.RemainingAmount < 0) Invoice.RemainingAmount = 0;

        _unitOfWork.PurchaseInvoices.Update(Invoice);
        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Payment of ${PayAmount:N2} processed successfully. Remaining balance updated.";
        return RedirectToPage(new { id });
    }
}
