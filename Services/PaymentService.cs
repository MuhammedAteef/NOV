using Stripe;
using SystemManagementSystem.Models;

namespace SystemManagementSystem.Services;

public class PaymentService : IPaymentService
{
    private readonly IConfiguration _config;

    public PaymentService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<(string clientSecret, string intentId)> CreateStripePaymentIntentAsync(
        decimal amount,
        string? idempotencyKey = null)
    {
        var amountInMinorUnits = ToMinorUnits(amount);
        StripeConfiguration.ApiKey = GetSecretKey();

        var options = new PaymentIntentCreateOptions
        {
            Amount = amountInMinorUnits,
            Currency = "usd",
            PaymentMethodTypes = new List<string> { "card" },
            CaptureMethod = "manual",
        };

        var service = new PaymentIntentService();
        var requestOptions = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : new RequestOptions { IdempotencyKey = idempotencyKey };
        var intent = await service.CreateAsync(options, requestOptions);
        return (intent.ClientSecret, intent.Id);
    }

    public async Task<bool> IsStripePaymentIntentReadyAsync(
        string? paymentIntentId,
        decimal expectedAmount)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId) ||
            expectedAmount <= 0 ||
            decimal.Round(expectedAmount, 2) != expectedAmount)
        {
            return false;
        }

        StripeConfiguration.ApiKey = GetSecretKey();
        var intent = await new PaymentIntentService().GetAsync(paymentIntentId);

        var expectedAmountInMinorUnits = ToMinorUnits(expectedAmount);
        return intent.Amount == expectedAmountInMinorUnits &&
            string.Equals(intent.Currency, "usd", StringComparison.OrdinalIgnoreCase) &&
            (intent.Status == "requires_capture" &&
                intent.AmountCapturable == expectedAmountInMinorUnits ||
                intent.Status == "succeeded" && intent.AmountReceived == expectedAmountInMinorUnits);
    }

    public async Task CaptureStripePaymentIntentAsync(string paymentIntentId, decimal expectedAmount)
    {
        StripeConfiguration.ApiKey = GetSecretKey();

        var service = new PaymentIntentService();
        var intent = await service.GetAsync(paymentIntentId);
        if (intent.Amount != ToMinorUnits(expectedAmount) ||
            !string.Equals(intent.Currency, "usd", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The Stripe payment amount does not match the invoice total.");
        }

        if (intent.Status == "succeeded")
        {
            if (intent.AmountReceived != ToMinorUnits(expectedAmount))
            {
                throw new InvalidOperationException("Stripe did not confirm the full invoice payment.");
            }

            return;
        }

        if (intent.Status != "requires_capture")
        {
            throw new InvalidOperationException("The Stripe payment is not authorized for capture.");
        }

        var capturedIntent = await service.CaptureAsync(paymentIntentId);
        if (capturedIntent.Status != "succeeded" ||
            capturedIntent.AmountReceived != ToMinorUnits(expectedAmount))
        {
            throw new InvalidOperationException("Stripe did not confirm the full invoice payment.");
        }
    }

    public async Task CancelStripePaymentIntentAsync(string paymentIntentId)
    {
        StripeConfiguration.ApiKey = GetSecretKey();
        var service = new PaymentIntentService();
        var intent = await service.GetAsync(paymentIntentId);
        if (intent.Status == "requires_capture")
        {
            await service.CancelAsync(paymentIntentId);
        }
    }

    public async Task RefundCapturedPaymentIntentAsync(string paymentIntentId)
    {
        StripeConfiguration.ApiKey = GetSecretKey();
        var service = new PaymentIntentService();
        var intent = await service.GetAsync(paymentIntentId);
        if (intent.Status != "succeeded")
        {
            return;
        }

        await new RefundService().CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId
        });
    }

    private string GetSecretKey()
    {
        var secretKey = _config["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Stripe:SecretKey is not configured.");
        }

        return secretKey;
    }

    private static long ToMinorUnits(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amounts must be positive and have at most two decimal places.");
        }

        return decimal.ToInt64(amount * 100m);
    }
}
