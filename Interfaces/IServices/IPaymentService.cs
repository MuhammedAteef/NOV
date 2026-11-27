using SystemManagementSystem.Models;

namespace SystemManagementSystem.Services;

public interface IPaymentService
{
    Task<(string clientSecret, string intentId)> CreateStripePaymentIntentAsync(
        decimal amount,
        string? idempotencyKey = null);

    Task<bool> IsStripePaymentIntentReadyAsync(string? paymentIntentId, decimal expectedAmount);

    Task CaptureStripePaymentIntentAsync(string paymentIntentId, decimal expectedAmount);

    Task CancelStripePaymentIntentAsync(string paymentIntentId);

    Task RefundCapturedPaymentIntentAsync(string paymentIntentId);
}
