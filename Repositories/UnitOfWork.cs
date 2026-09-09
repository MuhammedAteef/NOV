using SystemManagementSystem.Interfaces.IRepos;
using SystemManagementSystem.Models;

namespace SystemManagementSystem.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly SalesManagementSystemContext _context;

    public IRepository<Category> Categories { get; }
    public IRepository<Product> Products { get; }
    public IRepository<Customer> Customers { get; }
    public IRepository<Supplier> Suppliers { get; }
    public IPurchaseInvoiceRepository PurchaseInvoices { get; }
    public ISalesInvoiceRepository SalesInvoices { get; }
    public IRepository<PurchaseInvoiceItem> PurchaseInvoiceItems { get; }
    public IRepository<SalesInvoiceItem> SalesInvoiceItems { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<StockHistory> StockHistories { get; }

    public UnitOfWork(SalesManagementSystemContext context)
    {
        _context = context;
        Categories = new Repository<Category>(_context);
        Products = new Repository<Product>(_context);
        Customers = new Repository<Customer>(_context);
        Suppliers = new Repository<Supplier>(_context);
        PurchaseInvoices = new PurchaseInvoiceRepository(_context);
        SalesInvoices = new SalesInvoiceRepository(_context);
        PurchaseInvoiceItems = new Repository<PurchaseInvoiceItem>(_context);
        SalesInvoiceItems = new Repository<SalesInvoiceItem>(_context);
        Payments = new Repository<Payment>(_context);
        StockHistories = new Repository<StockHistory>(_context);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
