namespace SystemManagementSystem.Models;

public class PurchaseRefundItem
{
    public int Id { get; set; }

    public int PurchaseRefundId { get; set; }

    public int PurchaseInvoiceItemId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Total { get; set; }

    public PurchaseRefund PurchaseRefund { get; set; } = null!;
}
