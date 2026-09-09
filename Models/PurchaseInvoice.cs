using System;
using System.Collections.Generic;

namespace SystemManagementSystem.Models;

public partial class PurchaseInvoice
{
    public int PurchaseInvoiceId { get; set; }
    public int SupplierId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; } = 0;
    public decimal RemainingAmount { get; set; } = 0;

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; } = new List<PurchaseInvoiceItem>();

    public virtual Supplier Supplier { get; set; } = null!;
}
