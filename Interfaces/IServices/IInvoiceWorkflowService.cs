using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;

namespace SystemManagementSystem.Services;

public interface IInvoiceWorkflowService
{
    Task<SalesInvoice> CreateSalesInvoiceAsync(
        int customerId,
        DateTime invoiceDate,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        string paymentMethod,
        string requestKey,
        string? stripePaymentIntentId);

    Task<SalesReturn> CreateSalesReturnAsync(
        int invoiceId,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        string? reason,
        string requestKey);

    Task SoftDeleteSalesInvoiceAsync(int invoiceId);

    Task<PurchaseInvoice> CreatePurchaseInvoiceAsync(
        int supplierId,
        DateTime invoiceDate,
        decimal remainingAmount,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        string paymentMethod,
        string requestKey,
        string? stripePaymentIntentId);

    Task SoftDeletePurchaseInvoiceAsync(int invoiceId);

    Task<Payment> AddPurchasePaymentAsync(
        int invoiceId,
        decimal amount,
        string paymentMethod,
        string requestKey,
        string? stripePaymentIntentId);

    Task<PurchaseRefund> CreatePurchaseRefundAsync(
        int invoiceId,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        string? notes,
        string requestKey);
}
