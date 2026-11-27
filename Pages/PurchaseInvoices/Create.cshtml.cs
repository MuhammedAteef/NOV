using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
    private readonly IInvoiceWorkflowService _invoiceWorkflow;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IUnitOfWork unitOfWork,
        IConfiguration config,
        IPaymentService paymentService,
        IInvoiceWorkflowService invoiceWorkflow,
        ILogger<CreateModel> logger)
    {
        _unitOfWork = unitOfWork;
        _config = config;
        _paymentService = paymentService;
        _invoiceWorkflow = invoiceWorkflow;
        _logger = logger;
    }

    [BindProperty]
    public PurchaseInvoice Invoice { get; set; } = new();

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = new();

    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash";

    [BindProperty]
    public string? StripePaymentIntentId { get; set; }

    [BindProperty]
    public string RequestKey { get; set; } = "";

    public string StripePublishableKey { get; set; } = "";
    public SelectList SupplierList { get; set; } = default!;
    public List<object> ProductListData { get; set; } = [];

    public async Task OnGetAsync()
    {
        RequestKey = Guid.NewGuid().ToString("N");
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostCreateIntentAsync()
    {
        try
        {
            if (LineItems.Count == 0 || LineItems.Any(line => line.Quantity <= 0 || line.UnitPrice < 0))
            {
                throw new InvalidOperationException("Enter valid product quantities and purchase prices.");
            }

            var totalAmount = LineItems.Sum(line => line.Quantity * line.UnitPrice);
            var amountToPay = totalAmount - Invoice.RemainingAmount;
            if (amountToPay <= 0 || Invoice.RemainingAmount < 0 || Invoice.RemainingAmount > totalAmount)
            {
                throw new InvalidOperationException("The initial payment must be positive and cannot exceed the invoice total.");
            }

            var (clientSecret, intentId) = await _paymentService.CreateStripePaymentIntentAsync(amountToPay);
            return new JsonResult(new { clientSecret, intentId });
        }
        catch (InvalidOperationException exception)
        {
            return new JsonResult(new { error = exception.Message }) { StatusCode = 400 };
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

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadPageDataAsync();
        ModelState.Remove("Invoice.Supplier");
        ModelState.Remove("Invoice.PurchaseInvoiceItems");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var invoice = await _invoiceWorkflow.CreatePurchaseInvoiceAsync(
                Invoice.SupplierId,
                Invoice.InvoiceDate,
                Invoice.RemainingAmount,
                LineItems,
                PaymentMethod,
                RequestKey,
                StripePaymentIntentId);

            TempData["SuccessMessage"] = $"Purchase Invoice #{invoice.PurchaseInvoiceId} created successfully.";
            return RedirectToPage("./Show", new { id = invoice.PurchaseInvoiceId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Stripe payment processing failed for a purchase invoice.");
            ModelState.AddModelError(string.Empty, "Card payment could not be completed. No invoice was created.");
            return Page();
        }
    }

    private async Task LoadPageDataAsync()
    {
        StripePublishableKey = _config["Stripe:PublishableKey"] ?? "";
        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        var products = await _unitOfWork.Products.Query()
            .OrderBy(product => product.Name)
            .ToListAsync();
        SupplierList = new SelectList(suppliers, "SupplierId", "Name", Invoice.SupplierId);
        ProductListData = products.Select(product => new
        {
            value = product.ProductId,
            name = product.Name,
            text = product.Name,
            purchasePrice = product.PurchasePrice,
            stockQuantity = product.StockQuantity
        }).Cast<object>().ToList();
    }
}
