namespace SocksShoppingStore.Config
{
    public class PromoOptions
    {
        public List<PromoCode> Codes { get; set; } = new();
    }

    public enum PromoType
    {
        Percent,
        Fixed
    }

    public class PromoCode
    {
        public string Code { get; set; } = string.Empty;
        public PromoType Type { get; set; } = PromoType.Percent;
        public decimal Value { get; set; } = 0m; // percent or fixed amount
        public decimal? MinSubtotal { get; set; } // before tax, before shipping
        public DateTimeOffset? ExpiresUtc { get; set; }
        public int? MaxRedemptions { get; set; } // not enforced in-memory MVP
        public bool Active { get; set; } = true;
    }
}

