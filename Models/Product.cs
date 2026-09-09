using System;
using System.Collections.Generic;

namespace SystemManagementSystem.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal PurchasePrice { get; set; } = 0m;

    public decimal SellingPrice { get; set; } = 0m;

    public int StockQuantity { get; set; }

    public int CategoryId { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; } = new List<PurchaseInvoiceItem>();

    public virtual ICollection<SalesInvoiceItem> SalesInvoiceItems { get; set; } = new List<SalesInvoiceItem>();

    public virtual ICollection<StockHistory> StockHistories { get; set; } = new List<StockHistory>();
}
