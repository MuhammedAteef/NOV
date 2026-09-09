using Stripe;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;

    public PaymentService(IUnitOfWork unitOfWork, IConfiguration config)
    {
        _unitOfWork = unitOfWork;
        _config = config;
    }

    public async Task<(string clientSecret, string intentId)> CreateStripePaymentIntentAsync(decimal amount)
    {
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];

        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100),
            Currency = "usd",
            PaymentMethodTypes = new List<string> { "card" },
        };

        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(options);
        return (intent.ClientSecret, intent.Id);
    }

    public async Task<Models.Payment> ProcessInvoicePaymentAsync(
        int? salesInvoiceId,
        int? purchaseInvoiceId,
        decimal amount,
        string paymentMethod,
        string? stripePaymentIntentId = null)
    {
        if (paymentMethod != "Cash" && paymentMethod != "Card")
        {
            throw new ArgumentException("Invalid payment method. Expected 'Cash' or 'Card'.");
        }

        string refNum = (paymentMethod == "Card" ? stripePaymentIntentId ?? "" : $"CASH-{DateTime.Now:yyyyMMddHHmmss}");

        var payment = new Models.Payment
        {
            SalesInvoiceId = salesInvoiceId,
            PurchaseInvoiceId = purchaseInvoiceId,
            Amount = amount,
            PaymentDate = DateTime.Now,
            PaymentMethod = paymentMethod,
            Status = "Paid",
            ReferenceNumber = refNum,
            StripePaymentIntentId = stripePaymentIntentId,
        };

        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();
        return payment;
    }
    public async Task<string> PaymentStatusAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();

        var payIntent = await service.GetAsync(paymentIntentId);

        return payIntent.Status;
    }
}
