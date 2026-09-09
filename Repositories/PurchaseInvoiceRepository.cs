using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Interfaces.IRepos;
using SystemManagementSystem.Models;

namespace SystemManagementSystem.Repositories;

public class PurchaseInvoiceRepository : Repository<PurchaseInvoice>, IPurchaseInvoiceRepository
{
    public PurchaseInvoiceRepository(SalesManagementSystemContext context) : base(context)
    {
    }

    public async Task<List<PurchaseInvoice>> GetAllWithSupplierAsync()
    {
        return await _context.PurchaseInvoices
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.InvoiceDate)
            .ToListAsync();
    }

    public async Task<PurchaseInvoice?> GetWithDetailsAsync(int id)
    {
        return await _context.PurchaseInvoices
            .Include(i => i.Supplier)
            .Include(i => i.PurchaseInvoiceItems)
                .ThenInclude(ii => ii.Product)
            .SingleOrDefaultAsync(i => i.PurchaseInvoiceId == id);
    }

    public async Task SoftDeleteAsync(int id)
    {
        var invoice = await GetWithDetailsAsync(id);
        if (invoice == null || invoice.IsDeleted) return;

        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.Now;

        foreach (var item in invoice.PurchaseInvoiceItems)
        {
            item.IsDeleted = true;

            var product = item.Product;
            if (product != null)
            {
                product.StockQuantity -= item.Quantity;
            }

            _context.StockHistories.Add(new StockHistory
            {
                ProductId = item.ProductId,
                ChangeQuantity = item.Quantity * -1,
                MovementType = "OUT",
                MovementDate = DateTime.Now,
                Notes = $"Purchase Invoice #{id} deleted — stock reversed for {product?.Name}"
            });
        }

        await _context.SaveChangesAsync();
    }
}
