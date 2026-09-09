using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.SalesInvoices;

public class EditModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public EditModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public SalesInvoice Invoice { get; set; } = default!;

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = default!;
    public List<SalesInvoiceItem> ExistingItems { get; set; } = new();
    public SelectList CustomerList { get; set; } = default!;
    public SelectList ProductList { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        Invoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(id.Value);
        if (Invoice is null) return NotFound();

        ExistingItems = Invoice.SalesInvoiceItems.ToList();

        var customers = await _unitOfWork.Customers.GetAllAsync();
        var products  = await _unitOfWork.Products.GetAllAsync();
        CustomerList  = new SelectList(customers, "CustomerId", "Name", Invoice.CustomerId);
        ProductList   = new SelectList(products,  "ProductId",  "Name");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var customers = await _unitOfWork.Customers.GetAllAsync();
        var products  = await _unitOfWork.Products.GetAllAsync();
        CustomerList  = new SelectList(customers, "CustomerId", "Name", Invoice.CustomerId);
        ProductList   = new SelectList(products,  "ProductId",  "Name");

        ModelState.Remove("Invoice.Customer");
        ModelState.Remove("Invoice.SalesInvoiceItems");

        if (LineItems == null || !LineItems.Any())
        {
            ModelState.AddModelError(string.Empty, "Please add at least one product line item.");
            return Page();
        }

        var dbInvoice = await _unitOfWork.SalesInvoices.GetWithDetailsAsync(Invoice.SalesInvoiceId);
        if (dbInvoice == null) return NotFound();

        var dbItems = dbInvoice.SalesInvoiceItems.ToList();

        var submittedExistingIds = LineItems
            .Where(i => i.SalesItemId > 0)
            .Select(i => i.SalesItemId)
            .ToHashSet();

        // 1. DELETE removed rows
        var deletedItems = dbItems
            .Where(d => !submittedExistingIds.Contains(d.SalesItemId))
            .ToList();

        foreach (var deleted in deletedItems)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(deleted.ProductId);

            if (product != null)
                product.StockQuantity += deleted.Quantity;

            await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
            {
                ProductId = deleted.ProductId,
                ChangeQuantity = deleted.Quantity,
                MovementType = "IN",
                MovementDate = DateTime.Now,
                Notes = $"Sales Invoice #{dbInvoice.SalesInvoiceId} (Edited - Item Removed)"
            });

            _unitOfWork.SalesInvoiceItems.Remove(deleted);
        }

        // 2. UPDATE existing rows
        foreach (var line in LineItems.Where(l => l.SalesItemId > 0))
        {
            var dbItem = dbItems.FirstOrDefault(d => d.SalesItemId == line.SalesItemId);
            if (dbItem == null) continue;

            int stockDelta = dbItem.Quantity - line.Quantity;

            if (dbItem.ProductId != line.ProductId)
            {
                var oldProduct = await _unitOfWork.Products.GetByIdAsync(dbItem.ProductId);
                if (oldProduct != null)
                    oldProduct.StockQuantity += dbItem.Quantity;

                var newProduct = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
                if (newProduct != null)
                    newProduct.StockQuantity -= line.Quantity;

                await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
                {
                    ProductId = dbItem.ProductId,
                    ChangeQuantity = dbItem.Quantity,
                    MovementType = "IN",
                    MovementDate = DateTime.Now,
                    Notes = $"Sales Invoice #{dbInvoice.SalesInvoiceId} (Edited - Product Changed)"
                });

                await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
                {
                    ProductId = line.ProductId,
                    ChangeQuantity = -line.Quantity,
                    MovementType = "OUT",
                    MovementDate = DateTime.Now,
                    Notes = $"Sales Invoice #{dbInvoice.SalesInvoiceId} (Edited - Product Changed)"
                });
            }
            else if (stockDelta != 0)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
                if (product != null)
                    product.StockQuantity += stockDelta;

                await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
                {
                    ProductId = line.ProductId,
                    ChangeQuantity = stockDelta,
                    MovementType = stockDelta > 0 ? "IN" : "OUT",
                    MovementDate = DateTime.Now,
                    Notes = $"Sales Invoice #{dbInvoice.SalesInvoiceId} (Edited - Quantity Changed)"
                });
            }

            dbItem.ProductId = line.ProductId;
            dbItem.Quantity = line.Quantity;
            dbItem.UnitPrice = line.UnitPrice;
        }

        // 3. INSERT new rows
        foreach (var line in LineItems.Where(l => l.SalesItemId == 0))
        {
            var newItem = new SalesInvoiceItem
            {
                SalesInvoiceId = dbInvoice.SalesInvoiceId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            };

            await _unitOfWork.SalesInvoiceItems.AddAsync(newItem);

            var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
            if (product != null)
                product.StockQuantity -= line.Quantity;

            await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
            {
                ProductId = line.ProductId,
                ChangeQuantity = -line.Quantity,
                MovementType = "OUT",
                MovementDate = DateTime.Now,
                Notes = $"Sales Invoice #{dbInvoice.SalesInvoiceId} (Edited - New Item)"
            });
        }

        // 4. Update header & Save
        dbInvoice.CustomerId = Invoice.CustomerId;
        dbInvoice.InvoiceDate = Invoice.InvoiceDate == default
            ? dbInvoice.InvoiceDate
            : Invoice.InvoiceDate;

        dbInvoice.TotalAmount = LineItems.Sum(i => i.Quantity * i.UnitPrice);

        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Sales Invoice #{dbInvoice.SalesInvoiceId} updated successfully.";
        return RedirectToPage("./Index");
    }
}
