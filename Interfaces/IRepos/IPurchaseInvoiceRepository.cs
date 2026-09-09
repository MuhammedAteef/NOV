using SystemManagementSystem.Models;

namespace SystemManagementSystem.Interfaces.IRepos;

public interface IPurchaseInvoiceRepository : IRepository<PurchaseInvoice>
{
    Task<List<PurchaseInvoice>> GetAllWithSupplierAsync();
    Task<PurchaseInvoice?> GetWithDetailsAsync(int id);
    Task SoftDeleteAsync(int id);
}
