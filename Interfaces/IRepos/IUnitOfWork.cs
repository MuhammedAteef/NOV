using SystemManagementSystem.Models;

namespace SystemManagementSystem.Interfaces.IRepos;

public interface IUnitOfWork : IDisposable
{
    IRepository<Category> Categories { get; }
    IRepository<Product> Products { get; }
    IRepository<Customer> Customers { get; }
    IRepository<Supplier> Suppliers { get; }
    IPurchaseInvoiceRepository PurchaseInvoices { get; }
    ISalesInvoiceRepository SalesInvoices { get; }
    IRepository<PurchaseInvoiceItem> PurchaseInvoiceItems { get; }
    IRepository<SalesInvoiceItem> SalesInvoiceItems { get; }
    IRepository<Payment> Payments { get; }
    IRepository<StockHistory> StockHistories { get; }
    Task<int> SaveChangesAsync();
}
