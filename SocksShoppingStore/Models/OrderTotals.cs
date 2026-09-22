namespace SocksShoppingStore.Models
{
    public class OrderTotals
    {
        // Items subtotal excluding tax (net)
        public decimal SubtotalExTax { get; set; }
        // Items subtotal including tax (gross) before discounts
        public decimal SubtotalInclTax { get; set; }
        public decimal Discount { get; set; }
        public decimal Shipping { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalInclTax { get; set; }
        public decimal TaxRatePercent { get; set; }
        public string TaxLabel { get; set; } = "VAT";
        public bool IsEstimated { get; set; }
    }
}

