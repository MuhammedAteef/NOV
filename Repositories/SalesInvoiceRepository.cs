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
            .Include(s => s.Refunds)
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
}
