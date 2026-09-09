using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Stripe;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

public class CreateModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;
    private readonly IPaymentService _paymentService;

    public CreateModel(IUnitOfWork unitOfWork, IConfiguration config, IPaymentService paymentService)
    {
        _unitOfWork = unitOfWork;
        _config = config;
        _paymentService = paymentService;
    }

    [BindProperty]
    public PurchaseInvoice Invoice { get; set; } = default!;

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = new();

    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash"; 

    [BindProperty]
    public string? StripePaymentIntentId { get; set; }

    public string StripePublishableKey { get; set; } = "";
    public SelectList SupplierList { get; set; } = default!;
    public SelectList ProductList { get; set; } = default!;

    public async Task OnGetAsync()
    {
        StripePublishableKey = _config["Stripe:PublishableKey"] ?? "";
        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        var products  = await _unitOfWork.Products.GetAllAsync();
        SupplierList  = new SelectList(suppliers, "SupplierId", "Name");
        ProductList   = new SelectList(products,  "ProductId",  "Name");
    }

    // ── AJAX Handler for creating Stripe PaymentIntent directly from Create page ─────
    public async Task<IActionResult> OnPostCreateIntentAsync()
    {
        try
        {
            var (clientSecret, intentId) = await _paymentService.CreateStripePaymentIntentAsync(Invoice.PaidAmount);
            return new JsonResult(new { clientSecret, intentId });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 400 };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        StripePublishableKey = _config["Stripe:PublishableKey"] ?? "";
        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        var products  = await _unitOfWork.Products.GetAllAsync();
        SupplierList  = new SelectList(suppliers, "SupplierId", "Name");
        ProductList   = new SelectList(products,  "ProductId",  "Name");

        if (PaymentMethod != "Cash" && PaymentMethod != "Card")
        {
            ModelState.AddModelError(string.Empty, "Please select a valid payment method (Cash or Card). Unpaid is not permitted.");
            return Page();
        }

        if (PaymentMethod == "Card")
        {
            var paymentStatus = await _paymentService.PaymentStatusAsync(StripePaymentIntentId);
            if (paymentStatus != "succeeded")
            {
                ModelState.AddModelError("", "Card payment was not completed.");
                return Page();
            }
        }

        if (LineItems == null || !LineItems.Any())
        {
            ModelState.AddModelError(string.Empty, "Please add at least one product line item.");
            return Page();
        }

        // ── 1. Calculate total amount ───────────────────────────────────────
        Invoice.TotalAmount = LineItems.Sum(i => i.Quantity * i.UnitPrice);
        if(Invoice.PaidAmount > Invoice.TotalAmount)
        {
            ModelState.AddModelError(string.Empty, "Paid amount cannot exceed total amount.");
            return Page();
        }
        Invoice.PaidAmount = Invoice.TotalAmount - Invoice.RemainingAmount;
        Invoice.InvoiceDate = Invoice.InvoiceDate == default ? DateTime.Now : Invoice.InvoiceDate;

        // ── 2. Save the invoice header first to get the generated ID ────────
        await _unitOfWork.PurchaseInvoices.AddAsync(Invoice);
        await _unitOfWork.SaveChangesAsync();

        // ── 3. For each line item: create item record, stock history, update stock & calculate prices
        foreach (var line in LineItems)
        {
            var item = new PurchaseInvoiceItem
            {
                PurchaseInvoiceId = Invoice.PurchaseInvoiceId,
                ProductId         = line.ProductId,
                Quantity          = line.Quantity,
                UnitPrice         = line.UnitPrice
            };
            await _unitOfWork.PurchaseInvoiceItems.AddAsync(item);

            var stockEntry = new Models.StockHistory
            {
                ProductId      = line.ProductId,
                ChangeQuantity = line.Quantity,
                MovementType   = "IN",
                MovementDate   = DateTime.Now,
                Notes          = $"Purchase Invoice #{Invoice.PurchaseInvoiceId}"
            };
            await _unitOfWork.StockHistories.AddAsync(stockEntry);

            var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
            if (product != null)
            {
                product.StockQuantity += line.Quantity;
                product.PurchasePrice = line.UnitPrice;
                product.SellingPrice  = Math.Round(line.UnitPrice * 1.30m, 2); // 30% markup calculation
            }
        }

        // ── 4. Process Payment for Cash or Card selected via PaymentService ──
        await _paymentService.ProcessInvoicePaymentAsync(
            salesInvoiceId: null,
            purchaseInvoiceId: Invoice.PurchaseInvoiceId,
            amount: Invoice.PaidAmount,
            paymentMethod: PaymentMethod,
            stripePaymentIntentId: StripePaymentIntentId
        );

        // ── 5. Commit items + stock + price updates in one atomic operation 
        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Purchase Invoice #{Invoice.PurchaseInvoiceId} created successfully.";
        return RedirectToPage("./Show", new { id = Invoice.PurchaseInvoiceId });
    }
}
