using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;
using SystemManagementSystem.Services;

namespace SystemManagementSystem.Pages.SalesInvoices;

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
    public SalesInvoice Invoice { get; set; } = default!;

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = new();

    // Payment options on creation (Must be Cash or Card)
    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash";

    [BindProperty]
    public string? StripePaymentIntentId { get; set; }

    public string StripePublishableKey { get; set; } = "";
    public SelectList CustomerList { get; set; } = default!;
    public SelectList ProductList { get; set; } = default!;
    public List<object> ProductListData { get; set; } = new();

    public async Task OnGetAsync()
    {
        StripePublishableKey = _config["Stripe:PublishableKey"] ?? "";
        var customers = await _unitOfWork.Customers.GetAllAsync();
        var products = await _unitOfWork.Products.GetAllAsync();
        CustomerList = new SelectList(customers, "CustomerId", "Name");
        ProductList = new SelectList(products, "ProductId", "Name");

        ProductListData = products.Select(p => new
        {
            value = p.ProductId,
            text = $"{p.Name} (Stock: {p.StockQuantity}, ${p.SellingPrice:F2})",
            sellingPrice = p.SellingPrice,
            stockQuantity = p.StockQuantity
        }).Cast<object>().ToList();
    }

    // ── AJAX Handler for creating Stripe PaymentIntent directly from Create page ─────
    public async Task<IActionResult> OnPostCreateIntentAsync()
    {
        var amountStr = Request.Form["amount"].ToString();
        if (!decimal.TryParse(amountStr, out decimal amount) || amount <= 0)
        {
            return new JsonResult(new { error = "Invalid payment amount." }) { StatusCode = 400 };
        }

        try
        {
            var (clientSecret, intentId) = await _paymentService.CreateStripePaymentIntentAsync(amount);
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
        var customers = await _unitOfWork.Customers.GetAllAsync();
        var products = await _unitOfWork.Products.GetAllAsync();
        CustomerList = new SelectList(customers, "CustomerId", "Name");
        ProductList = new SelectList(products, "ProductId", "Name");

        ProductListData = products.Select(p => new
        {
            value = p.ProductId,
            text = $"{p.Name} (Stock: {p.StockQuantity}, ${p.SellingPrice:F2})",
            sellingPrice = p.SellingPrice,
            stockQuantity = p.StockQuantity
        }).Cast<object>().ToList();

        if (PaymentMethod != "Cash" && PaymentMethod != "Card")
        {
            ModelState.AddModelError(string.Empty, "Please select a valid payment method (Cash or Card). Unpaid status is not permitted.");
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

        // ── Validate Stock Quantity for each requested product line ──
        foreach (var line in LineItems)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
            if (product == null)
            {
                ModelState.AddModelError(string.Empty, "Selected product does not exist.");
                return Page();
            }
            if (line.Quantity <= 0)
            {
                ModelState.AddModelError(string.Empty, $"Quantity for '{product.Name}' must be greater than 0.");
                return Page();
            }
            if (line.Quantity > product.StockQuantity)
            {
                ModelState.AddModelError(string.Empty, $"Cannot sell {line.Quantity} units of '{product.Name}'. Only {product.StockQuantity} units available in stock.");
                return Page();
            }
        }

        Invoice.TotalAmount = LineItems.Sum(line => line.Quantity * line.UnitPrice);
        Invoice.InvoiceDate = Invoice.InvoiceDate == default ? DateTime.Now : Invoice.InvoiceDate;

        await _unitOfWork.SalesInvoices.AddAsync(Invoice);
        await _unitOfWork.SaveChangesAsync();

        foreach (var line in LineItems)
        {
            var item = new SalesInvoiceItem
            {
                SalesInvoiceId = Invoice.SalesInvoiceId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            };
            await _unitOfWork.SalesInvoiceItems.AddAsync(item);

            var stockEntry = new Models.StockHistory
            {
                ProductId = line.ProductId,
                ChangeQuantity = line.Quantity * -1,
                MovementType = "OUT",
                MovementDate = DateTime.Now,
                Notes = $"Sales Invoice #{Invoice.SalesInvoiceId}"
            };
            await _unitOfWork.StockHistories.AddAsync(stockEntry);

            var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
            if (product is not null)
            {
                product.StockQuantity -= line.Quantity;
            }
        }

        // ── Process Payment for Cash or Card selected via PaymentService ──
        await _paymentService.ProcessInvoicePaymentAsync(
            salesInvoiceId: Invoice.SalesInvoiceId,
            purchaseInvoiceId: null,
            amount: Invoice.TotalAmount,
            paymentMethod: PaymentMethod,
            stripePaymentIntentId: StripePaymentIntentId
        );

        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Sales Invoice #{Invoice.SalesInvoiceId} created successfully.";
        return RedirectToPage("./Show", new { id = Invoice.SalesInvoiceId });
    }
}
