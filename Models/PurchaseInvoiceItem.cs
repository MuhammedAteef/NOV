using System;
using System.Collections.Generic;

namespace SystemManagementSystem.Models;

public partial class PurchaseInvoiceItem
{
    public int PurchaseItemId { get; set; }

    public int PurchaseInvoiceId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? SubTotal { get; set; }

    public bool IsDeleted { get; set; } = false;

    public virtual Product Product { get; set; } = null!;

    public virtual PurchaseInvoice PurchaseInvoice { get; set; } = null!;
}
