namespace SocksShoppingStore.Config
{
    public class FeatureFlags
    {
        public bool EnableTaxes { get; set; } = true;
        public bool EnableShipping { get; set; } = true;
        public bool EnablePromoCodes { get; set; } = true;
        public bool EnablePdfInvoices { get; set; } = true;
        // When true, features are active only in Development environment
        public bool OnlyInDevelopment { get; set; } = true;
    }
}

