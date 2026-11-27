using SystemManagementSystem.Models;

namespace SystemManagementSystem.Interfaces.IRepos;

public interface ISalesInvoiceRepository : IRepository<SalesInvoice>
{
    Task<List<SalesInvoice>> GetAllWithCustomerAsync();
    Task<SalesInvoice?> GetWithDetailsAsync(int id);
}
