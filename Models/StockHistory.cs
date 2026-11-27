using System;
using System.Collections.Generic;

namespace SystemManagementSystem.Models;

public partial class StockHistory
{
    public int StockHistoryId { get; set; }

    public int ProductId { get; set; }

    public int ChangeQuantity { get; set; }

    public string MovementType { get; set; } = null!;

    public DateTime MovementDate { get; set; }

    public string? Notes { get; set; }

    public int? SalesInvoiceId { get; set; }

    public int? PurchaseInvoiceId { get; set; }

    public int? SalesReturnId { get; set; }

    public int? PurchaseRefundId { get; set; }

    public virtual Product Product { get; set; } = null!;
}
