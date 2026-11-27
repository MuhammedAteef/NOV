namespace SystemManagementSystem.Models;

public class SalesReturn
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }

    public DateTime ReturnDate { get; set; }
    public string? Reason { get; set; }
    public string? RequestKey { get; set; }

    public decimal TotalAmount { get; set; }

    public ReturnStatus Status { get; set; }

    public ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();

    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
