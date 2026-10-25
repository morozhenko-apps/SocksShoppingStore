using Microsoft.Extensions.Options;
using SocksShoppingStore.Config;
using SocksShoppingStore.Models;
using SocksShoppingStore.Services;

namespace SocksShoppingStore.Tests;

[TestFixture]
[Category("Unit")]
public class PromoClockTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public int Reads { get; private set; }
        public override DateTimeOffset GetUtcNow() { Reads++; return Now; }
    }

    private static TotalsCalculator Calculator(Clock clock, PromoCode promo) => new(
        Options.Create(new TaxOptions { DefaultRatePercent = 0m }),
        Options.Create(new ShippingOptions()),
        Options.Create(new PromoOptions { Codes = new() { promo } }), clock);

    private static readonly OrderItem[] Items = { new() { UnitPrice = 50m, Quantity = 2 } };

    [TestCase(-1, 0)]
    [TestCase(0, 10)]
    [TestCase(1, 10)]
    public void Given_ExpiryBoundary_When_Computed_Then_ExistingInclusiveRuleIsPreserved(long expiryTicks, decimal discount)
    {
        // Arrange
        var clock = new Clock();
        var promo = new PromoCode { Code = "SAVE", Type = PromoType.Fixed, Value = 10m, ExpiresUtc = clock.Now.AddTicks(expiryTicks) };
        var subject = Calculator(clock, promo);
        // Act
        var result = subject.Compute(Items, null, "save", true);
        // Assert
        Assert.Multiple(() => {
            Assert.That(result.totals.Discount, Is.EqualTo(discount));
            Assert.That(result.totals.TotalInclTax, Is.EqualTo(100m - discount));
            Assert.That(result.totals.IsEstimated, Is.True);
            Assert.That(result.appliedPromo, Is.EqualTo("SAVE"));
            Assert.That(clock.Reads, Is.EqualTo(1));
        });
    }

    [TestCase(false, false, 0)]
    [TestCase(false, true, 0)]
    [TestCase(true, false, 10)]
    public void Given_InactiveOrUnboundedPromo_When_Computed_Then_ClockIsNotNeeded(bool active, bool hasExpiry, decimal discount)
    {
        // Arrange
        var clock = new Clock();
        var promo = new PromoCode { Code = "SAVE", Value = 10m, Type = PromoType.Fixed, Active = active,
            ExpiresUtc = hasExpiry ? clock.Now.AddDays(1) : null };
        var subject = Calculator(clock, promo);
        // Act
        var result = subject.Compute(Items, null, "SAVE", false);
        // Assert
        Assert.That(result.totals.Discount, Is.EqualTo(discount));
        Assert.That(result.totals.TotalInclTax, Is.EqualTo(100m - discount));
        Assert.That(clock.Reads, Is.Zero);
    }

    [TestCase(-12)]
    [TestCase(0)]
    [TestCase(14)]
    public void Given_EquivalentExpiryWithDifferentOffset_When_Computed_Then_UtcInstantDeterminesEligibility(int offset)
    {
        // Arrange
        var clock = new Clock();
        var promo = new PromoCode { Code = "SAVE", Value = 10m, Type = PromoType.Fixed,
            ExpiresUtc = clock.Now.ToOffset(TimeSpan.FromHours(offset)) };
        var subject = Calculator(clock, promo);
        // Act
        var result = subject.Compute(Items, null, "SAVE", false);
        // Assert
        Assert.That(result.totals.Discount, Is.EqualTo(10m));
        Assert.That(result.totals.TotalInclTax, Is.EqualTo(90m));
        Assert.That(clock.Reads, Is.EqualTo(1));
    }

    [Test]
    public void Given_ClockMovesForwardThenBack_When_Recomputed_Then_NoExpiryResultIsCached()
    {
        // Arrange
        var clock = new Clock();
        var expiry = clock.Now;
        var subject = Calculator(clock, new PromoCode { Code = "SAVE", Value = 10m, Type = PromoType.Fixed, ExpiresUtc = expiry });
        // Act
        var atExpiry = subject.Compute(Items, null, "SAVE", false).totals;
        clock.Now = expiry.AddTicks(1);
        var after = subject.Compute(Items, null, "SAVE", false).totals;
        clock.Now = expiry.AddTicks(-1);
        var before = subject.Compute(Items, null, "SAVE", false).totals;
        // Assert
        Assert.That(new[] { atExpiry.Discount, after.Discount, before.Discount }, Is.EqualTo(new[] { 10m, 0m, 10m }));
        Assert.That(new[] { atExpiry.TotalInclTax, after.TotalInclTax, before.TotalInclTax }, Is.EqualTo(new[] { 90m, 100m, 90m }));
        Assert.That(clock.Reads, Is.EqualTo(3));
    }
}
