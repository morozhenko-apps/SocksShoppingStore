namespace SocksShoppingStore.Config
{
    public class ShippingOptions
    {
        public List<ShippingMethod> Methods { get; set; } = new();
        public string? Default { get; set; }
    }

    public class ShippingMethod
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool Taxable { get; set; } = true;
    }
}

