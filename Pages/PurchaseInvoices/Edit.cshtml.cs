using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;
using SystemManagementSystem.Repositories;

namespace SystemManagementSystem.Pages.PurchaseInvoices;

public class EditModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;

    public EditModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public PurchaseInvoice Invoice { get; set; } = default!;

    [BindProperty]
    public List<InvoiceLineItemDTO> LineItems { get; set; } = new();

    public SelectList SupplierList { get; set; } = default!;
    public SelectList ProductList  { get; set; } = default!;

    public List<PurchaseInvoiceItem> ExistingItems { get; set; } = new();
    [BindProperty]
    public decimal changedAmount { get; set; }


    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        Invoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(id.Value);

        if (Invoice is null) return NotFound();

        ExistingItems = Invoice.PurchaseInvoiceItems.ToList();

        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        var products  = await _unitOfWork.Products.GetAllAsync();
        SupplierList  = new SelectList(suppliers, "SupplierId", "Name", Invoice.SupplierId);
        ProductList   = new SelectList(products,  "ProductId",  "Name");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var suppliers = await _unitOfWork.Suppliers.GetAllAsync();
        var products  = await _unitOfWork.Products.GetAllAsync();
        SupplierList  = new SelectList(suppliers, "SupplierId", "Name", Invoice.SupplierId);
        ProductList   = new SelectList(products,  "ProductId",  "Name");

        ModelState.Remove("Invoice.Supplier");
        ModelState.Remove("Invoice.PurchaseInvoiceItems");

        if (LineItems == null || !LineItems.Any())
        {
            ModelState.AddModelError(string.Empty, "Please add at least one product line item.");
            return Page();
        }

        var dbInvoice = await _unitOfWork.PurchaseInvoices.GetWithDetailsAsync(Invoice.PurchaseInvoiceId);
        if (dbInvoice is null) return NotFound();

        var dbItems = dbInvoice.PurchaseInvoiceItems.ToList();

        var submittedExistingIds = LineItems
            .Where(l => l.PurchaseItemId > 0)
            .Select(l => l.PurchaseItemId)
            .ToHashSet();

        // 1. DELETE removed items
        var deletedItems = dbItems.Where(d => !submittedExistingIds.Contains(d.PurchaseItemId)).ToList();
        foreach (var deleted in deletedItems)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(deleted.ProductId);
            if (product != null)
                product.StockQuantity -= deleted.Quantity;

            await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
            {
                ProductId = deleted.ProductId,
                ChangeQuantity = deleted.Quantity * -1,
                MovementType = "OUT",
                MovementDate = DateTime.Now,
                Notes = $"Purchase Invoice #{dbInvoice.PurchaseInvoiceId} (Edited - Item Removed)"
            });

            // removed item change accounted by recomputing totals later
            _unitOfWork.PurchaseInvoiceItems.Remove(deleted);
        }

        // 2. UPDATE existing rows
        foreach (var line in LineItems.Where(l => l.PurchaseItemId > 0))
        {
            var dbItem = dbItems.FirstOrDefault(d => d.PurchaseItemId == line.PurchaseItemId);
            if (dbItem is null) continue;

            int stockDelta = line.Quantity - dbItem.Quantity;

            if (dbItem.ProductId != line.ProductId)
            {
                var oldProduct = await _unitOfWork.Products.GetByIdAsync(dbItem.ProductId);
                if (oldProduct != null)
                    oldProduct.StockQuantity -= dbItem.Quantity;

                var newProduct = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
                if (newProduct != null)
                {
                    newProduct.StockQuantity += line.Quantity;
                    newProduct.PurchasePrice  = line.UnitPrice;
                    newProduct.SellingPrice   = Math.Round(line.UnitPrice * 1.30m, 2);
                }

                await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
                {
                    ProductId = dbItem.ProductId,
                    ChangeQuantity = -dbItem.Quantity,
                    MovementType = "OUT",
                    MovementDate = DateTime.Now,
                    Notes = $"Purchase Invoice #{dbInvoice.PurchaseInvoiceId} (Edited - Product Changed)"
                });

                await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
                {
                    ProductId = line.ProductId,
                    ChangeQuantity = line.Quantity,
                    MovementType = "IN",
                    MovementDate = DateTime.Now,
                    Notes = $"Purchase Invoice #{dbInvoice.PurchaseInvoiceId} (Edited - Product Changed)"
                });
            }
            else if (stockDelta != 0)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
                if (product != null)
                {
                    product.StockQuantity += stockDelta;
                    product.PurchasePrice  = line.UnitPrice;
                    product.SellingPrice   = Math.Round(line.UnitPrice * 1.30m, 2);
                }

                await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
                {
                    ProductId = line.ProductId,
                    ChangeQuantity = stockDelta,
                    MovementType = stockDelta > 0 ? "IN" : "OUT",
                    MovementDate = DateTime.Now,
                    Notes = $"Purchase Invoice #{dbInvoice.PurchaseInvoiceId} (Edited - Quantity Changed)"
                });
            }
            else
            {
                var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
                if (product != null)
                {
                    product.PurchasePrice = line.UnitPrice;
                    product.SellingPrice  = Math.Round(line.UnitPrice * 1.30m, 2);
                }
            }

            dbItem.ProductId  = line.ProductId;
            dbItem.Quantity   = line.Quantity;
            dbItem.UnitPrice  = line.UnitPrice;
        }

        // 3. INSERT new rows
        foreach (var line in LineItems.Where(l => l.PurchaseItemId == 0))
        {
            var newItem = new PurchaseInvoiceItem
            {
                PurchaseInvoiceId = dbInvoice.PurchaseInvoiceId,
                ProductId         = line.ProductId,
                Quantity          = line.Quantity,
                UnitPrice         = line.UnitPrice
            };
            await _unitOfWork.PurchaseInvoiceItems.AddAsync(newItem);

            await _unitOfWork.StockHistories.AddAsync(new Models.StockHistory
            {
                ProductId      = line.ProductId,
                ChangeQuantity = line.Quantity,
                MovementType   = "IN",
                MovementDate   = DateTime.Now,
                Notes          = $"Purchase Invoice #{dbInvoice.PurchaseInvoiceId} (Edited — new item)"
            });

            var product = await _unitOfWork.Products.GetByIdAsync(line.ProductId);
            if (product != null)
            {
                product.StockQuantity += line.Quantity;
                product.PurchasePrice  = line.UnitPrice;
                product.SellingPrice   = Math.Round(line.UnitPrice * 1.30m, 2);
            }
        }

        // 4. Update header & commit
        dbInvoice.SupplierId  = Invoice.SupplierId;
        dbInvoice.InvoiceDate = Invoice.InvoiceDate == default ? dbInvoice.InvoiceDate : Invoice.InvoiceDate;

        //var originalTotal = dbInvoice.TotalAmount;
        var newTotal = LineItems.Sum(i => i.Quantity * i.UnitPrice);
        dbInvoice.TotalAmount = newTotal;

        dbInvoice.PaidAmount = Invoice.PaidAmount;

        dbInvoice.RemainingAmount = Math.Max(0, newTotal - dbInvoice.PaidAmount);

        await _unitOfWork.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Purchase Invoice #{dbInvoice.PurchaseInvoiceId} updated successfully.";
        return RedirectToPage("./Index");
    }
}
