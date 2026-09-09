using System;
using System.Collections.Generic;

namespace SystemManagementSystem.Models;

public partial class SalesInvoiceItem
{
    public int SalesItemId { get; set; }

    public int SalesInvoiceId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? SubTotal { get; set; }

    public bool IsDeleted { get; set; } = false;

    public virtual Product Product { get; set; } = null!;

    public virtual SalesInvoice SalesInvoice { get; set; } = null!;
}
