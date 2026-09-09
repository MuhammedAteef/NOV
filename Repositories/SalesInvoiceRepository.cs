using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.Interfaces.IRepos;
using SystemManagementSystem.Models;

namespace SystemManagementSystem.Repositories;

public class SalesInvoiceRepository : Repository<SalesInvoice>, ISalesInvoiceRepository
{
    public SalesInvoiceRepository(SalesManagementSystemContext context) : base(context)
    {
    }

    public async Task<List<SalesInvoice>> GetAllWithCustomerAsync()
    {
        return await _context.SalesInvoices
            .Include(s => s.Customer)
            .OrderByDescending(s => s.InvoiceDate)
            .ToListAsync();
    }

    public async Task<SalesInvoice?> GetWithDetailsAsync(int id)
    {
        return await _context.SalesInvoices
            .Include(i => i.Customer)
            .Include(i => i.SalesInvoiceItems)
                .ThenInclude(ii => ii.Product)
            .SingleOrDefaultAsync(i => i.SalesInvoiceId == id);
    }

    public async Task SoftDeleteAsync(int id)
    {
        var invoice = await GetWithDetailsAsync(id);
        if (invoice == null || invoice.IsDeleted) return;

        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.Now;

        foreach (var item in invoice.SalesInvoiceItems)
        {
            item.IsDeleted = true;

            var product = item.Product;
            if (product != null)
            {
                product.StockQuantity += item.Quantity;
            }

            _context.StockHistories.Add(new StockHistory
            {
                ProductId = item.ProductId,
                ChangeQuantity = item.Quantity,
                MovementType = "IN",
                MovementDate = DateTime.Now,
                Notes = $"Sales Invoice #{id} deleted — stock restored for {product?.Name}"
            });
        }

        await _context.SaveChangesAsync();
    }
}
