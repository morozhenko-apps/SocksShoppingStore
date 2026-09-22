using Microsoft.Extensions.Options;
using SocksShoppingStore.Config;
using SocksShoppingStore.Models;

namespace SocksShoppingStore.Services
{
    public class TotalsCalculator
    {
        private readonly TaxOptions _taxOptions;
        private readonly ShippingOptions _shippingOptions;
        private readonly PromoOptions _promoOptions;

        public TotalsCalculator(IOptions<TaxOptions> tax, IOptions<ShippingOptions> shipping, IOptions<PromoOptions> promo)
        {
            _taxOptions = tax.Value;
            _shippingOptions = shipping.Value;
            _promoOptions = promo.Value;
        }

        public (OrderTotals totals, string appliedShipping, string? appliedPromo) Compute(IEnumerable<OrderItem> items, string? shippingCode, string? promoCode, bool estimated)
        {
            var list = items.ToList();
            var rate = Math.Max(0m, _taxOptions.DefaultRatePercent) / 100m;

            // Items gross (current prices are treated as gross when PricesIncludeTax)
            var itemsGross = list.Sum(i => i.Subtotal);

            // Shipping (gross)
            var ship = ResolveShipping(shippingCode);
            var shippingGross = ship?.Price ?? 0m;
            var shippingTaxable = ship?.Taxable ?? true;

            // Discount (apply to items only)
            var promo = ResolvePromo(promoCode);
            var discountGross = 0m;
            if (promo != null && IsPromoActive(promo))
            {
                var meetsMin = !promo.MinSubtotal.HasValue || itemsGross >= promo.MinSubtotal.Value;
                if (meetsMin)
                {
                    discountGross = promo.Type == PromoType.Percent ? Math.Round(itemsGross * (promo.Value / 100m), 2, MidpointRounding.AwayFromZero) : promo.Value;
                    if (discountGross > itemsGross) discountGross = itemsGross;
                }
            }

            decimal itemsNet, discountNet, shippingNet, itemsTax, discountTax, shippingTax;

            if (_taxOptions.PricesIncludeTax)
            {
                itemsNet = rate > 0m ? Round(itemsGross / (1m + rate)) : itemsGross;
                discountNet = rate > 0m ? Round(discountGross / (1m + rate)) : discountGross;
                shippingNet = (shippingTaxable && rate > 0m) ? Round(shippingGross / (1m + rate)) : shippingGross;

                itemsTax = itemsGross - itemsNet;
                discountTax = discountGross - discountNet;
                shippingTax = shippingTaxable ? (shippingGross - shippingNet) : 0m;
            }
            else
            {
                itemsNet = itemsGross;
                discountNet = discountGross;
                shippingNet = shippingGross;

                var taxableBase = (itemsNet - discountNet) + (shippingTaxable ? shippingNet : 0m);
                var totalTax = Round(taxableBase * rate);

                // Split approximate tax shares (for transparency)
                var itemsShare = itemsNet > 0 ? (itemsNet - discountNet) / Math.Max(0.01m, taxableBase) : 0m;
                itemsTax = Round(totalTax * itemsShare);
                shippingTax = shippingTaxable ? (totalTax - itemsTax) : 0m;
                discountTax = 0m; // discount in net-pricing reduces taxable base, not separate tax
            }

            var totals = new OrderTotals
            {
                SubtotalExTax = itemsNet,
                SubtotalInclTax = itemsGross,
                Discount = discountGross,
                Shipping = shippingGross,
                TaxAmount = Round(itemsTax - discountTax + shippingTax),
                TotalInclTax = Round(itemsGross - discountGross + shippingGross),
                TaxRatePercent = _taxOptions.DefaultRatePercent,
                TaxLabel = _taxOptions.Label,
                IsEstimated = estimated
            };

            return (totals, ship?.Code ?? string.Empty, promo?.Code);
        }

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private ShippingMethod? ResolveShipping(string? code)
        {
            if (_shippingOptions.Methods == null || _shippingOptions.Methods.Count == 0) return null;
            if (!string.IsNullOrWhiteSpace(code))
            {
                var found = _shippingOptions.Methods.FirstOrDefault(m => string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }
            if (!string.IsNullOrWhiteSpace(_shippingOptions.Default))
            {
                return _shippingOptions.Methods.FirstOrDefault(m => string.Equals(m.Code, _shippingOptions.Default, StringComparison.OrdinalIgnoreCase)) ?? _shippingOptions.Methods.First();
            }
            return _shippingOptions.Methods.First();
        }

        private PromoCode? ResolvePromo(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            return _promoOptions.Codes?.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPromoActive(PromoCode promo)
        {
            if (!promo.Active) return false;
            if (promo.ExpiresUtc.HasValue && promo.ExpiresUtc.Value < DateTimeOffset.UtcNow) return false;
            return true;
        }
    }
}

