namespace SystemManagementSystem.DTO
{
    public class InvoiceLineItemDTO
    {
        public int PurchaseItemId { get; set; }
        public int SalesItemId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
