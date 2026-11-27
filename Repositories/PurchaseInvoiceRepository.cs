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
            .Include(p => p.Refunds)
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
}
