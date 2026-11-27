namespace SystemManagementSystem.Models;

public class SalesReturnItem
{
    public int Id { get; set; }
    public int SalesReturnId { get; set; }

    public int InvoiceItemId { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }

    public SalesReturn SalesReturn { get; set; } = null!;
}
