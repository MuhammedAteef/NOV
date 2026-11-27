namespace SystemManagementSystem.Models;

public class Payment
{
    public int PaymentId { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ReferenceNumber { get; set; }

    public string? StripePaymentIntentId { get; set; }
    public string? IdempotencyKey { get; set; }

    public int? SalesInvoiceId { get; set; }
    public int? PurchaseInvoiceId { get; set; }

    public virtual SalesInvoice? SalesInvoice { get; set; }
    public virtual PurchaseInvoice? PurchaseInvoice { get; set; }
}
