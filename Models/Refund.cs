namespace SystemManagementSystem.Models;

public class Refund
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public SalesInvoice Invoice { get; set; } = null!;

    public int SalesReturnId { get; set; }
    public SalesReturn SalesReturn { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime RefundDate { get; set; }

    public RefundMethod Method { get; set; }

    public RefundStatus Status { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }
}
