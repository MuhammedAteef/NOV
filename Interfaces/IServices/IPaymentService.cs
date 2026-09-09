using SystemManagementSystem.Models;

namespace SystemManagementSystem.Services;

public interface IPaymentService
{
    Task<(string clientSecret, string intentId)> CreateStripePaymentIntentAsync(decimal amount);
    Task<Payment> ProcessInvoicePaymentAsync(int? salesInvoiceId, int? purchaseInvoiceId, decimal amount, string paymentMethod, string? stripePaymentIntentId = null);
    Task<string> PaymentStatusAsync(string stripePaymentIntentId);
}
