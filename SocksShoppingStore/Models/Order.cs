namespace SocksShoppingStore.Models
{
    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Customer info
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        // Items
        public List<OrderItem> Items { get; set; } = new();

        public decimal Total => Items.Sum(i => i.Subtotal);
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Extended totals (breakdown). Nullable until computed.
        public OrderTotals? Totals { get; set; }
    }
}
