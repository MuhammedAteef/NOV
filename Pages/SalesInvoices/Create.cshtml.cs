using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Stripe;
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
    public SalesInvoice Invoice { get; set; } = new();

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = new();

    [BindProperty]
    public string PaymentMethod { get; set; } = "Cash";

    [BindProperty]
    public string? StripePaymentIntentId { get; set; }

    [BindProperty]
    public string RequestKey { get; set; } = "";

    public string StripePublishableKey { get; set; } = "";
    public SelectList CustomerList { get; set; } = default!;
    public SelectList ProductList { get; set; } = default!;
    public List<object> ProductListData { get; set; } = new();

    public async Task OnGetAsync()
    {
        RequestKey = Guid.NewGuid().ToString("N");
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostCreateIntentAsync()
    {
        try
        {
            var total = await CalculateServerTotalAsync();
            var (clientSecret, intentId) = await _paymentService.CreateStripePaymentIntentAsync(total);
            return new JsonResult(new { clientSecret, intentId });
        }
        catch (InvalidOperationException exception)
        {
            return new JsonResult(new { error = exception.Message }) { StatusCode = 400 };
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Stripe could not authorize a sales invoice payment.");
            return new JsonResult(new { error = "Card authorization failed. Please try again." })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadPageDataAsync();
        ModelState.Remove("Invoice.Customer");
        ModelState.Remove("Invoice.SalesInvoiceItems");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var invoice = await _invoiceWorkflow.CreateSalesInvoiceAsync(
                Invoice.CustomerId,
                Invoice.InvoiceDate,
                LineItems,
                PaymentMethod,
                RequestKey,
                StripePaymentIntentId);

            TempData["SuccessMessage"] = $"Sales Invoice #{invoice.SalesInvoiceId} created and fully paid.";
            return RedirectToPage("./Show", new { id = invoice.SalesInvoiceId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Stripe payment processing failed for a sales invoice.");
            ModelState.AddModelError(string.Empty, "Card payment could not be completed. No invoice was created.");
            return Page();
        }
    }

    private async Task LoadPageDataAsync()
    {
        StripePublishableKey = _config["Stripe:PublishableKey"] ?? "";
        var customers = await _unitOfWork.Customers.GetAllAsync();
        var products = await _unitOfWork.Products.GetAllAsync();
        CustomerList = new SelectList(customers, "CustomerId", "Name", Invoice.CustomerId);
        ProductList = new SelectList(products, "ProductId", "Name");
        ProductListData = products.Select(product => new
        {
            value = product.ProductId,
            text = $"{product.Name} (Stock: {product.StockQuantity}, ${product.SellingPrice:F2})",
            sellingPrice = product.SellingPrice,
            stockQuantity = product.StockQuantity
        }).Cast<object>().ToList();
    }

    private async Task<decimal> CalculateServerTotalAsync()
    {
        if (LineItems.Count == 0 || LineItems.Any(line => line.ProductId <= 0 || line.Quantity <= 0))
        {
            throw new InvalidOperationException("Select at least one product and enter a positive quantity.");
        }

        var productIds = LineItems.Select(line => line.ProductId).Distinct().ToArray();
        var products = await _unitOfWork.Products.Query()
            .Where(product => productIds.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId);
        if (products.Count != productIds.Length)
        {
            throw new InvalidOperationException("One or more selected products do not exist.");
        }

        return LineItems.Sum(line => products[line.ProductId].SellingPrice * line.Quantity);
    }
}
