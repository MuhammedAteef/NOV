namespace SystemManagementSystem.Models;

public class PurchaseRefund
{
    public int Id { get; set; }

    public int PurchaseInvoiceId { get; set; }

    public DateTime RefundDate { get; set; }

    public decimal TotalAmount { get; set; }

    public RefundStatus Status { get; set; }

    public string? RequestKey { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;

    public ICollection<PurchaseRefundItem> Items { get; set; } = new List<PurchaseRefundItem>();
}
