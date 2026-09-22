namespace SocksShoppingStore.Config
{
    public class TaxOptions
    {
        public bool Enabled { get; set; } = true;
        // \"Vat\" or \"SalesTax\" (MVP: Vat)
        public string Mode { get; set; } = "Vat";
        // For EU-like pricing we show prices including tax
        public bool PricesIncludeTax { get; set; } = true;
        public decimal DefaultRatePercent { get; set; } = 20m;
        public string Label { get; set; } = "VAT";
    }
}

