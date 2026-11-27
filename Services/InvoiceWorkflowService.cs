using System.Data;
using Microsoft.EntityFrameworkCore;
using SystemManagementSystem.DTO;
using SystemManagementSystem.Models;

namespace SystemManagementSystem.Services;

public class InvoiceWorkflowService : IInvoiceWorkflowService
{
    private readonly SalesManagementSystemContext _context;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<InvoiceWorkflowService> _logger;

    public InvoiceWorkflowService(
        SalesManagementSystemContext context,
        IPaymentService paymentService,
        ILogger<InvoiceWorkflowService> logger)
    {
        _context = context;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<SalesInvoice> CreateSalesInvoiceAsync(
        int customerId,
        DateTime invoiceDate,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        string paymentMethod,
        string requestKey,
        string? stripePaymentIntentId)
    {
        ValidateRequestKey(requestKey);
        ValidatePaymentMethod(paymentMethod);
        var quantities = NormalizeQuantities(lineItems, line => line.ProductId, line => line.Quantity);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var intentAuthorized = false;
        var captureAttempted = false;

        try
        {
            var existingInvoice = await _context.SalesInvoices
                .SingleOrDefaultAsync(invoice => invoice.RequestKey == requestKey);
            if (existingInvoice is not null)
            {
                var existingLines = await _context.SalesInvoiceItems
                    .AsNoTracking()
                    .Where(item => item.SalesInvoiceId == existingInvoice.SalesInvoiceId)
                    .ToDictionaryAsync(item => item.ProductId, item => item.Quantity);
                var existingPayment = await _context.Payments
                    .AsNoTracking()
                    .SingleOrDefaultAsync(payment => payment.SalesInvoiceId == existingInvoice.SalesInvoiceId);
                if (existingInvoice.CustomerId != customerId ||
                    !SameQuantities(existingLines, quantities) ||
                    existingPayment is null ||
                    existingPayment.PaymentMethod != paymentMethod ||
                    existingPayment.StripePaymentIntentId != stripePaymentIntentId)
                {
                    throw new InvalidOperationException("This transaction request key has already been used for a different sales invoice.");
                }

                await transaction.RollbackAsync();
                return existingInvoice;
            }

            if (!await _context.Customers.AnyAsync(customer => customer.CustomerId == customerId))
            {
                throw new InvalidOperationException("The selected customer does not exist.");
            }

            var products = await LoadProductsAsync(quantities.Keys);
            var totalAmount = quantities.Sum(item => products[item.Key].SellingPrice * item.Value);
            ValidateInvoiceTotal(totalAmount);

            if (paymentMethod == "Card")
            {
                if (!await _paymentService.IsStripePaymentIntentReadyAsync(stripePaymentIntentId, totalAmount))
                {
                    throw new InvalidOperationException(
                        "Card authorization must match the full invoice total and be ready for capture.");
                }

                intentAuthorized = true;
            }
            else if (!string.IsNullOrWhiteSpace(stripePaymentIntentId))
            {
                throw new InvalidOperationException("A Stripe payment intent cannot be used for a cash payment.");
            }

            var invoice = new SalesInvoice
            {
                CustomerId = customerId,
                InvoiceDate = invoiceDate == default ? DateTime.Now : invoiceDate,
                TotalAmount = totalAmount,
                RequestKey = requestKey,
                SalesInvoiceItems = quantities.Select(item => new SalesInvoiceItem
                {
                    ProductId = item.Key,
                    Quantity = item.Value,
                    UnitPrice = products[item.Key].SellingPrice
                }).ToList()
            };

            foreach (var item in quantities)
            {
                var changedRows = await _context.Products
                    .Where(product => product.ProductId == item.Key && product.StockQuantity >= item.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(product => product.StockQuantity, product => product.StockQuantity - item.Value));

                if (changedRows != 1)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock for '{products[item.Key].Name}'. No invoice was created.");
                }
            }

            _context.SalesInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            _context.StockHistories.AddRange(quantities.Select(item => new StockHistory
            {
                ProductId = item.Key,
                ChangeQuantity = -item.Value,
                MovementType = "OUT",
                MovementDate = DateTime.Now,
                Notes = $"Sales Invoice #{invoice.SalesInvoiceId}",
                SalesInvoiceId = invoice.SalesInvoiceId
            }));

            _context.Payments.Add(CreatePayment(
                totalAmount,
                paymentMethod,
                requestKey,
                stripePaymentIntentId,
                invoice.SalesInvoiceId,
                null));
            await _context.SaveChangesAsync();

            if (paymentMethod == "Card")
            {
                captureAttempted = true;
                await _paymentService.CaptureStripePaymentIntentAsync(stripePaymentIntentId!, totalAmount);
            }

            await transaction.CommitAsync();
            return invoice;
        }
        catch (Exception exception)
        {
            await RollBackAsync(transaction, exception);
            if (intentAuthorized)
            {
                await CompensateStripeIntentAsync(stripePaymentIntentId!, captureAttempted);
            }

            throw;
        }
    }

    public async Task<SalesReturn> CreateSalesReturnAsync(
        int invoiceId,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        string? reason,
        string requestKey)
    {
        ValidateRequestKey(requestKey);
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = null;
        }
        else
        {
            reason = reason.Trim();
            if (reason.Length > 500)
            {
                throw new InvalidOperationException("The return reason cannot exceed 500 characters.");
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existingReturn = await _context.SalesReturns
            .Include(salesReturn => salesReturn.Refunds)
            .SingleOrDefaultAsync(salesReturn => salesReturn.RequestKey == requestKey);
        if (existingReturn is not null)
        {
            await transaction.RollbackAsync();
            return existingReturn;
        }

        var invoice = await _context.SalesInvoices
            .Include(salesInvoice => salesInvoice.SalesInvoiceItems)
            .SingleOrDefaultAsync(salesInvoice => salesInvoice.SalesInvoiceId == invoiceId);
        if (invoice is null || invoice.IsDeleted)
        {
            throw new InvalidOperationException("The original sales invoice was not found or is deleted.");
        }

        var paidAmount = await _context.Payments
            .Where(payment => payment.SalesInvoiceId == invoiceId && payment.Status == "Paid")
            .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
        if (paidAmount != invoice.TotalAmount)
        {
            throw new InvalidOperationException("Only a fully paid sales invoice can be returned.");
        }

        var selections = await ValidateSalesReturnLinesAsync(invoice, lineItems);
        var totalAmount = selections.Sum(selection => selection.Item.UnitPrice * selection.Quantity);
        if (totalAmount <= 0)
        {
            throw new InvalidOperationException("Select at least one product quantity to return.");
        }

        var salesReturn = new SalesReturn
        {
            InvoiceId = invoiceId,
            ReturnDate = DateTime.Now,
            Reason = reason,
            TotalAmount = totalAmount,
            Status = ReturnStatus.Completed,
            RequestKey = requestKey,
            Items = selections.Select(selection => new SalesReturnItem
            {
                InvoiceItemId = selection.Item.SalesItemId,
                Quantity = selection.Quantity,
                UnitPrice = selection.Item.UnitPrice,
                Total = selection.Item.UnitPrice * selection.Quantity
            }).ToList()
        };

        var refund = new Refund
        {
            InvoiceId = invoiceId,
            SalesReturn = salesReturn,
            Amount = totalAmount,
            RefundDate = salesReturn.ReturnDate,
            Method = RefundMethod.Cash,
            Status = RefundStatus.Completed,
            Notes = reason
        };
        salesReturn.Refunds.Add(refund);
        _context.SalesReturns.Add(salesReturn);

        foreach (var group in selections.GroupBy(selection => selection.Item.ProductId))
        {
            var quantity = group.Sum(selection => selection.Quantity);
            var changedRows = await _context.Products
                .Where(product => product.ProductId == group.Key &&
                    product.StockQuantity <= int.MaxValue - quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(product => product.StockQuantity, product => product.StockQuantity + quantity));

            if (changedRows != 1)
            {
                throw new InvalidOperationException("The returned quantity cannot be added to inventory.");
            }
        }

        await _context.SaveChangesAsync();

        _context.StockHistories.AddRange(selections
            .GroupBy(selection => selection.Item.ProductId)
            .Select(group => new StockHistory
            {
                ProductId = group.Key,
                ChangeQuantity = group.Sum(selection => selection.Quantity),
                MovementType = "IN",
                MovementDate = salesReturn.ReturnDate,
                Notes = $"Sales Refund for Invoice #{invoiceId}",
                SalesReturnId = salesReturn.Id
            }));

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return salesReturn;
    }

    public async Task SoftDeleteSalesInvoiceAsync(int invoiceId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await _context.SalesInvoices
            .Include(salesInvoice => salesInvoice.SalesInvoiceItems)
            .SingleOrDefaultAsync(salesInvoice => salesInvoice.SalesInvoiceId == invoiceId);
        if (invoice is null || invoice.IsDeleted)
        {
            throw new InvalidOperationException("The sales invoice was not found or has already been deleted.");
        }

        var returnedQuantities = await _context.SalesReturnItems
            .Where(item => item.SalesReturn.InvoiceId == invoiceId &&
                item.SalesReturn.Status == ReturnStatus.Completed)
            .GroupBy(item => item.InvoiceItemId)
            .Select(group => new { ItemId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(group => group.ItemId, group => group.Quantity);

        var stockToRestore = new Dictionary<int, long>();
        foreach (var item in invoice.SalesInvoiceItems.Where(item => !item.IsDeleted))
        {
            var returnedQuantity = returnedQuantities.GetValueOrDefault(item.SalesItemId);
            if (returnedQuantity < 0 || returnedQuantity > item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Returned quantities for sales invoice item #{item.SalesItemId} are inconsistent.");
            }

            var remainingQuantity = item.Quantity - returnedQuantity;
            if (remainingQuantity > 0)
            {
                stockToRestore[item.ProductId] =
                    stockToRestore.GetValueOrDefault(item.ProductId) + remainingQuantity;
            }
        }

        foreach (var (productId, quantity) in stockToRestore)
        {
            if (quantity > int.MaxValue)
            {
                throw new InvalidOperationException("The sales invoice stock reversal exceeds the supported quantity.");
            }

            var quantityToRestore = (int)quantity;
            var changedRows = await _context.Products
                .Where(product => product.ProductId == productId &&
                    product.StockQuantity <= int.MaxValue - quantityToRestore)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(product => product.StockQuantity, product => product.StockQuantity + quantityToRestore));

            if (changedRows != 1)
            {
                throw new InvalidOperationException(
                    $"Stock for product #{productId} cannot be increased to reverse this sales invoice.");
            }

            _context.StockHistories.Add(new StockHistory
            {
                ProductId = productId,
                ChangeQuantity = quantityToRestore,
                MovementType = "IN",
                MovementDate = DateTime.Now,
                Notes = $"Admin reversal for Sales Invoice #{invoiceId}"
            });
        }

        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.Now;
        foreach (var item in invoice.SalesInvoiceItems)
        {
            item.IsDeleted = true;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<PurchaseInvoice> CreatePurchaseInvoiceAsync(
        int supplierId,
        DateTime invoiceDate,
        decimal remainingAmount,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        string paymentMethod,
        string requestKey,
        string? stripePaymentIntentId)
    {
        ValidateRequestKey(requestKey);
        ValidatePaymentMethod(paymentMethod);
        var quantities = NormalizeQuantities(lineItems, line => line.ProductId, line => line.Quantity);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var intentAuthorized = false;
        var captureAttempted = false;

        try
        {
            var existingInvoice = await _context.PurchaseInvoices
                .SingleOrDefaultAsync(invoice => invoice.RequestKey == requestKey);
            if (existingInvoice is not null)
            {
                var existingLines = await _context.PurchaseInvoiceItems
                    .AsNoTracking()
                    .Where(item => item.PurchaseInvoiceId == existingInvoice.PurchaseInvoiceId)
                    .OrderBy(item => item.ProductId)
                    .ThenBy(item => item.UnitPrice)
                    .ThenBy(item => item.Quantity)
                    .Select(item => new { item.ProductId, item.Quantity, item.UnitPrice })
                    .ToListAsync();
                var requestedLines = lineItems
                    .OrderBy(item => item.ProductId)
                    .ThenBy(item => item.UnitPrice)
                    .ThenBy(item => item.Quantity)
                    .Select(item => new { item.ProductId, item.Quantity, item.UnitPrice })
                    .ToList();
                var existingPayment = await _context.Payments
                    .AsNoTracking()
                    .SingleOrDefaultAsync(payment => payment.PurchaseInvoiceId == existingInvoice.PurchaseInvoiceId);
                if (existingInvoice.SupplierId != supplierId ||
                    existingInvoice.RemainingAmount != remainingAmount ||
                    !existingLines.SequenceEqual(requestedLines) ||
                    existingPayment is null ||
                    existingPayment.PaymentMethod != paymentMethod ||
                    existingPayment.StripePaymentIntentId != stripePaymentIntentId)
                {
                    throw new InvalidOperationException("This transaction request key has already been used for a different purchase invoice.");
                }

                await transaction.RollbackAsync();
                return existingInvoice;
            }

            if (!await _context.Suppliers.AnyAsync(supplier => supplier.SupplierId == supplierId))
            {
                throw new InvalidOperationException("The selected supplier does not exist.");
            }

            if (lineItems.Any(line => line.Quantity <= 0))
            {
                throw new InvalidOperationException("Purchase quantities must be positive.");
            }

            foreach (var line in lineItems)
            {
                if (line.UnitPrice < 0 || decimal.Round(line.UnitPrice, 2) != line.UnitPrice)
                {
                    throw new InvalidOperationException("Purchase prices must be non-negative and use at most two decimal places.");
                }
            }

            var totalAmount = lineItems.Sum(line => line.Quantity * line.UnitPrice);
            ValidateInvoiceTotal(totalAmount);
            if (remainingAmount < 0 || remainingAmount > totalAmount ||
                decimal.Round(remainingAmount, 2) != remainingAmount)
            {
                throw new InvalidOperationException("The remaining amount must be between zero and the invoice total.");
            }

            var paidAmount = totalAmount - remainingAmount;
            if (paidAmount <= 0)
            {
                throw new InvalidOperationException("A purchase invoice must include an initial payment.");
            }

            var products = await LoadProductsAsync(quantities.Keys);
            if (paymentMethod == "Card")
            {
                if (!await _paymentService.IsStripePaymentIntentReadyAsync(stripePaymentIntentId, paidAmount))
                {
                    throw new InvalidOperationException(
                        "Card authorization must match the purchase payment amount and be ready for capture.");
                }

                intentAuthorized = true;
            }
            else if (!string.IsNullOrWhiteSpace(stripePaymentIntentId))
            {
                throw new InvalidOperationException("A Stripe payment intent cannot be used for a cash payment.");
            }

            var invoice = new PurchaseInvoice
            {
                SupplierId = supplierId,
                InvoiceDate = invoiceDate == default ? DateTime.Now : invoiceDate,
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                RemainingAmount = remainingAmount,
                RequestKey = requestKey,
                PurchaseInvoiceItems = lineItems.Select(line => new PurchaseInvoiceItem
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice
                }).ToList()
            };

            foreach (var item in quantities)
            {
                var changedRows = await _context.Products
                    .Where(product => product.ProductId == item.Key &&
                        product.StockQuantity <= int.MaxValue - item.Value)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(product => product.StockQuantity, product => product.StockQuantity + item.Value));

                if (changedRows != 1)
                {
                    throw new InvalidOperationException(
                        $"The inventory quantity for '{products[item.Key].Name}' would exceed the supported limit.");
                }

                products[item.Key].PurchasePrice = lineItems
                    .Where(line => line.ProductId == item.Key)
                    .Select(line => line.UnitPrice)
                    .Last();
                products[item.Key].SellingPrice = Math.Round(products[item.Key].PurchasePrice * 1.30m, 2);
            }

            _context.PurchaseInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            _context.StockHistories.AddRange(quantities.Select(item => new StockHistory
            {
                ProductId = item.Key,
                ChangeQuantity = item.Value,
                MovementType = "IN",
                MovementDate = invoice.InvoiceDate,
                Notes = $"Purchase Invoice #{invoice.PurchaseInvoiceId}",
                PurchaseInvoiceId = invoice.PurchaseInvoiceId
            }));

            _context.Payments.Add(CreatePayment(
                paidAmount,
                paymentMethod,
                requestKey,
                stripePaymentIntentId,
                null,
                invoice.PurchaseInvoiceId));
            await _context.SaveChangesAsync();

            if (paymentMethod == "Card")
            {
                captureAttempted = true;
                await _paymentService.CaptureStripePaymentIntentAsync(stripePaymentIntentId!, paidAmount);
            }

            await transaction.CommitAsync();
            return invoice;
        }
        catch (Exception exception)
        {
            await RollBackAsync(transaction, exception);
            if (intentAuthorized)
            {
                await CompensateStripeIntentAsync(stripePaymentIntentId!, captureAttempted);
            }

            throw;
        }
    }

    public async Task<Payment> AddPurchasePaymentAsync(
        int invoiceId,
        decimal amount,
        string paymentMethod,
        string requestKey,
        string? stripePaymentIntentId)
    {
        ValidateRequestKey(requestKey);
        ValidatePaymentMethod(paymentMethod);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var intentAuthorized = false;
        var captureAttempted = false;

        try
        {
            var existingPayment = await _context.Payments
                .SingleOrDefaultAsync(payment => payment.IdempotencyKey == requestKey);
            if (existingPayment is not null)
            {
                if (existingPayment.PurchaseInvoiceId != invoiceId ||
                    existingPayment.Amount != amount ||
                    existingPayment.PaymentMethod != paymentMethod ||
                    existingPayment.StripePaymentIntentId != stripePaymentIntentId)
                {
                    throw new InvalidOperationException("This payment request key has already been used.");
                }

                await transaction.RollbackAsync();
                return existingPayment;
            }

            var invoice = await _context.PurchaseInvoices
                .SingleOrDefaultAsync(item => item.PurchaseInvoiceId == invoiceId);
            if (invoice is null || invoice.IsDeleted)
            {
                throw new InvalidOperationException("The purchase invoice was not found or is deleted.");
            }

            var paidAmount = await _context.Payments
                .Where(payment => payment.PurchaseInvoiceId == invoiceId && payment.Status == "Paid")
                .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
            var remainingAmount = invoice.TotalAmount - paidAmount;
            if (amount <= 0 || amount > remainingAmount ||
                decimal.Round(amount, 2) != amount)
            {
                throw new InvalidOperationException("The payment must be positive and cannot exceed the remaining invoice balance.");
            }

            if (paymentMethod == "Card")
            {
                if (!await _paymentService.IsStripePaymentIntentReadyAsync(stripePaymentIntentId, amount))
                {
                    throw new InvalidOperationException(
                        "Card authorization must match the payment amount and be ready for capture.");
                }

                intentAuthorized = true;
            }
            else if (!string.IsNullOrWhiteSpace(stripePaymentIntentId))
            {
                throw new InvalidOperationException("A Stripe payment intent cannot be used for a cash payment.");
            }

            var payment = CreatePayment(
                amount,
                paymentMethod,
                requestKey,
                stripePaymentIntentId,
                null,
                invoiceId);
            _context.Payments.Add(payment);
            invoice.PaidAmount = paidAmount + amount;
            invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;

            await _context.SaveChangesAsync();

            if (paymentMethod == "Card")
            {
                captureAttempted = true;
                await _paymentService.CaptureStripePaymentIntentAsync(stripePaymentIntentId!, amount);
            }

            await transaction.CommitAsync();
            return payment;
        }
        catch (Exception exception)
        {
            await RollBackAsync(transaction, exception);
            if (intentAuthorized)
            {
                await CompensateStripeIntentAsync(stripePaymentIntentId!, captureAttempted);
            }

            throw;
        }
    }

    public async Task SoftDeletePurchaseInvoiceAsync(int invoiceId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await _context.PurchaseInvoices
            .Include(purchaseInvoice => purchaseInvoice.PurchaseInvoiceItems)
            .SingleOrDefaultAsync(purchaseInvoice => purchaseInvoice.PurchaseInvoiceId == invoiceId);
        if (invoice is null || invoice.IsDeleted)
        {
            throw new InvalidOperationException("The purchase invoice was not found or has already been deleted.");
        }

        var refundedQuantities = await _context.PurchaseRefundItems
            .Where(item => item.PurchaseRefund.PurchaseInvoiceId == invoiceId &&
                item.PurchaseRefund.Status == RefundStatus.Completed)
            .GroupBy(item => item.PurchaseInvoiceItemId)
            .Select(group => new { ItemId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(group => group.ItemId, group => group.Quantity);

        var stockToRemove = new Dictionary<int, long>();
        foreach (var item in invoice.PurchaseInvoiceItems.Where(item => !item.IsDeleted))
        {
            var refundedQuantity = refundedQuantities.GetValueOrDefault(item.PurchaseItemId);
            if (refundedQuantity < 0 || refundedQuantity > item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Refunded quantities for purchase invoice item #{item.PurchaseItemId} are inconsistent.");
            }

            var remainingQuantity = item.Quantity - refundedQuantity;
            if (remainingQuantity > 0)
            {
                stockToRemove[item.ProductId] =
                    stockToRemove.GetValueOrDefault(item.ProductId) + remainingQuantity;
            }
        }

        foreach (var (productId, quantity) in stockToRemove)
        {
            if (quantity > int.MaxValue)
            {
                throw new InvalidOperationException("The purchase invoice stock reversal exceeds the supported quantity.");
            }

            var quantityToRemove = (int)quantity;
            var changedRows = await _context.Products
                .Where(product => product.ProductId == productId && product.StockQuantity >= quantityToRemove)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(product => product.StockQuantity, product => product.StockQuantity - quantityToRemove));

            if (changedRows != 1)
            {
                throw new InvalidOperationException(
                    $"There is not enough stock for product #{productId} to reverse this purchase invoice.");
            }

            _context.StockHistories.Add(new StockHistory
            {
                ProductId = productId,
                ChangeQuantity = -quantityToRemove,
                MovementType = "OUT",
                MovementDate = DateTime.Now,
                Notes = $"Admin reversal for Purchase Invoice #{invoiceId}"
            });
        }

        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.Now;
        foreach (var item in invoice.PurchaseInvoiceItems)
        {
            item.IsDeleted = true;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<PurchaseRefund> CreatePurchaseRefundAsync(
        int invoiceId,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        string? notes,
        string requestKey)
    {
        ValidateRequestKey(requestKey);
        if (!string.IsNullOrWhiteSpace(notes) && notes.Length > 500)
        {
            throw new InvalidOperationException("Refund notes cannot exceed 500 characters.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existingRefund = await _context.PurchaseRefunds
            .SingleOrDefaultAsync(refund => refund.RequestKey == requestKey);
        if (existingRefund is not null)
        {
            await transaction.RollbackAsync();
            return existingRefund;
        }

        var invoice = await _context.PurchaseInvoices
            .Include(purchaseInvoice => purchaseInvoice.PurchaseInvoiceItems)
            .SingleOrDefaultAsync(purchaseInvoice => purchaseInvoice.PurchaseInvoiceId == invoiceId);
        if (invoice is null || invoice.IsDeleted)
        {
            throw new InvalidOperationException("The original purchase invoice was not found or is deleted.");
        }

        var invoiceItems = invoice.PurchaseInvoiceItems.ToDictionary(item => item.PurchaseItemId);
        if (lineItems.Any(line => line.PurchaseItemId <= 0 || line.Quantity < 0))
        {
            throw new InvalidOperationException("Purchased item IDs must be positive and refund quantities cannot be negative.");
        }

        var submittedItems = lineItems
            .Where(line => line.Quantity > 0)
            .ToList();
        if (submittedItems.Count == 0 ||
            submittedItems.GroupBy(line => line.PurchaseItemId).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Select each purchased product line at most once with a positive return quantity.");
        }

        var previouslyRefunded = await _context.PurchaseRefundItems
            .Where(item => item.PurchaseRefund.PurchaseInvoiceId == invoiceId &&
                item.PurchaseRefund.Status == RefundStatus.Completed)
            .GroupBy(item => item.PurchaseInvoiceItemId)
            .Select(group => new { ItemId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(group => group.ItemId, group => group.Quantity);

        var selections = new List<(PurchaseInvoiceItem Item, int Quantity)>();
        foreach (var submitted in submittedItems)
        {
            if (!invoiceItems.TryGetValue(submitted.PurchaseItemId, out var item))
            {
                throw new InvalidOperationException("A selected product does not belong to this purchase invoice.");
            }

            var remainingQuantity = item.Quantity - previouslyRefunded.GetValueOrDefault(item.PurchaseItemId);
            if (submitted.Quantity < 0 || submitted.Quantity > remainingQuantity)
            {
                throw new InvalidOperationException(
                    $"The refund quantity for purchased item #{item.PurchaseItemId} exceeds the remaining refundable quantity.");
            }

            selections.Add((item, submitted.Quantity));
        }

        var totalAmount = selections.Sum(selection => selection.Item.UnitPrice * selection.Quantity);
        if (totalAmount <= 0)
        {
            throw new InvalidOperationException("Select at least one product quantity to refund.");
        }

        foreach (var group in selections.GroupBy(selection => selection.Item.ProductId))
        {
            var quantity = group.Sum(selection => selection.Quantity);
            var changedRows = await _context.Products
                .Where(product => product.ProductId == group.Key && product.StockQuantity >= quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(product => product.StockQuantity, product => product.StockQuantity - quantity));

            if (changedRows != 1)
            {
                throw new InvalidOperationException("The stock on hand is insufficient to return the purchased quantity to the supplier.");
            }
        }

        var purchaseRefund = new PurchaseRefund
        {
            PurchaseInvoiceId = invoiceId,
            RefundDate = DateTime.Now,
            TotalAmount = totalAmount,
            Status = RefundStatus.Completed,
            RequestKey = requestKey,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Items = selections.Select(selection => new PurchaseRefundItem
            {
                PurchaseInvoiceItemId = selection.Item.PurchaseItemId,
                Quantity = selection.Quantity,
                UnitPrice = selection.Item.UnitPrice,
                Total = selection.Item.UnitPrice * selection.Quantity
            }).ToList()
        };
        _context.PurchaseRefunds.Add(purchaseRefund);
        await _context.SaveChangesAsync();

        _context.StockHistories.AddRange(selections
            .GroupBy(selection => selection.Item.ProductId)
            .Select(group => new StockHistory
            {
                ProductId = group.Key,
                ChangeQuantity = -group.Sum(selection => selection.Quantity),
                MovementType = "OUT",
                MovementDate = purchaseRefund.RefundDate,
                Notes = $"Purchase Refund for Invoice #{invoiceId}",
                PurchaseRefundId = purchaseRefund.Id
            }));

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return purchaseRefund;
    }

    private async Task<Dictionary<int, Product>> LoadProductsAsync(IEnumerable<int> productIds)
    {
        var ids = productIds.ToArray();
        var products = await _context.Products
            .Where(product => ids.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId);

        if (products.Count != ids.Length)
        {
            throw new InvalidOperationException("One or more selected products do not exist.");
        }

        return products;
    }

    private async Task<List<(SalesInvoiceItem Item, int Quantity)>> ValidateSalesReturnLinesAsync(
        SalesInvoice invoice,
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems)
    {
        if (lineItems.Any(line => line.Quantity < 0))
        {
            throw new InvalidOperationException("Return quantities cannot be negative.");
        }

        var requestedItems = lineItems
            .Where(line => line.Quantity > 0)
            .ToList();
        if (requestedItems.Count == 0 ||
            requestedItems.GroupBy(line => line.SalesItemId).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Select each sold product line at most once with a positive return quantity.");
        }

        var invoiceItems = invoice.SalesInvoiceItems
            .Where(item => !item.IsDeleted)
            .ToDictionary(item => item.SalesItemId);
        var previouslyReturned = await _context.SalesReturnItems
            .Where(item => item.SalesReturn.InvoiceId == invoice.SalesInvoiceId &&
                item.SalesReturn.Status == ReturnStatus.Completed)
            .GroupBy(item => item.InvoiceItemId)
            .Select(group => new { ItemId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToDictionaryAsync(group => group.ItemId, group => group.Quantity);

        var selections = new List<(SalesInvoiceItem Item, int Quantity)>();
        foreach (var requested in requestedItems)
        {
            if (!invoiceItems.TryGetValue(requested.SalesItemId, out var item))
            {
                throw new InvalidOperationException("A selected product does not belong to this sales invoice.");
            }

            var remainingQuantity = item.Quantity - previouslyReturned.GetValueOrDefault(item.SalesItemId);
            if (requested.Quantity > remainingQuantity)
            {
                throw new InvalidOperationException(
                    $"The return quantity for '{item.ProductId}' exceeds the remaining refundable quantity.");
            }

            selections.Add((item, requested.Quantity));
        }

        return selections;
    }

    private Payment CreatePayment(
        decimal amount,
        string paymentMethod,
        string requestKey,
        string? stripePaymentIntentId,
        int? salesInvoiceId,
        int? purchaseInvoiceId)
    {
        if (!string.IsNullOrWhiteSpace(stripePaymentIntentId) &&
            _context.Payments.Any(payment => payment.StripePaymentIntentId == stripePaymentIntentId))
        {
            throw new InvalidOperationException("This Stripe payment has already been recorded.");
        }

        return new Payment
        {
            Amount = amount,
            PaymentDate = DateTime.Now,
            PaymentMethod = paymentMethod,
            Status = "Paid",
            ReferenceNumber = paymentMethod == "Card"
                ? stripePaymentIntentId
                : $"CASH-{Guid.NewGuid():N}",
            StripePaymentIntentId = stripePaymentIntentId,
            IdempotencyKey = requestKey,
            SalesInvoiceId = salesInvoiceId,
            PurchaseInvoiceId = purchaseInvoiceId
        };
    }

    private async Task CompensateStripeIntentAsync(string paymentIntentId, bool captureAttempted)
    {
        try
        {
            if (captureAttempted)
            {
                await _paymentService.RefundCapturedPaymentIntentAsync(paymentIntentId);
            }

            await _paymentService.CancelStripePaymentIntentAsync(paymentIntentId);
        }
        catch (Exception compensationException)
        {
            _logger.LogCritical(
                compensationException,
                "Could not compensate Stripe payment intent {PaymentIntentId} after a failed invoice transaction.",
                paymentIntentId);
            throw new InvalidOperationException(
                "The database transaction failed and automatic Stripe compensation also failed. Reconcile this payment before retrying.",
                compensationException);
        }
    }

    private async Task RollBackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Exception exception)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(rollbackException, "Database rollback failed after invoice workflow error.");
            exception.Data["RollbackError"] = rollbackException.Message;
        }
    }

    private static Dictionary<int, int> NormalizeQuantities(
        IReadOnlyCollection<InvoiceLineItemDTO> lineItems,
        Func<InvoiceLineItemDTO, int> getProductId,
        Func<InvoiceLineItemDTO, int> getQuantity)
    {
        if (lineItems.Count == 0)
        {
            throw new InvalidOperationException("At least one product line item is required.");
        }

        if (lineItems.Any(line => getProductId(line) <= 0 || getQuantity(line) <= 0))
        {
            throw new InvalidOperationException("Product IDs and quantities must be positive.");
        }

        try
        {
            return lineItems
                .GroupBy(getProductId)
                .ToDictionary(group => group.Key, group => checked(group.Sum(getQuantity)));
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException("The combined product quantity exceeds the supported limit.", exception);
        }
    }

    private static bool SameQuantities(
        IReadOnlyDictionary<int, int> existingQuantities,
        IReadOnlyDictionary<int, int> requestedQuantities)
    {
        return existingQuantities.Count == requestedQuantities.Count &&
            existingQuantities.All(item =>
                requestedQuantities.TryGetValue(item.Key, out var quantity) &&
                quantity == item.Value);
    }

    private static void ValidateRequestKey(string requestKey)
    {
        if (!Guid.TryParseExact(requestKey, "N", out _))
        {
            throw new InvalidOperationException("The transaction request key is invalid. Reload the form and try again.");
        }
    }

    private static void ValidatePaymentMethod(string paymentMethod)
    {
        if (paymentMethod is not ("Cash" or "Card"))
        {
            throw new InvalidOperationException("Select Cash or Card as the payment method.");
        }
    }

    private static void ValidateInvoiceTotal(decimal totalAmount)
    {
        if (totalAmount <= 0 || decimal.Round(totalAmount, 2) != totalAmount)
        {
            throw new InvalidOperationException("The invoice total must be positive and use at most two decimal places.");
        }
    }
}
