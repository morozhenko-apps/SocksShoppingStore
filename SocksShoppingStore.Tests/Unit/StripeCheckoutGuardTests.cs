using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocksShoppingStore.Config;
using SocksShoppingStore.Models;
using SocksShoppingStore.Services;
using Stripe.Checkout;

namespace SocksShoppingStore.Tests;

[TestFixture]
[Category("Unit")]
public class StripeCheckoutGuardTests
{
    private static Order Draft() => new()
    {
        Items = new List<OrderItem>
        {
            new() { Name = "Synthetic socks", Quantity = 2, UnitPrice = 12.345m }
        }
    };

    private static HttpRequest Request()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("shop.example.test");
        return request;
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Given_MissingKey_When_CheckoutRequested_Then_NoSdkCreationOccurs(string? key)
    {
        // Arrange
        var calls = 0;
        var service = new StripeCheckoutService(
            Options.Create(new StripeOptions { SecretKey = key! }),
            NullLogger<StripeCheckoutService>.Instance,
            (_, _) => { calls++; return Task.FromResult(new Session()); });
        // Act
        var failure = Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateCheckoutSessionAsync(Draft(), Request()));
        // Assert
        Assert.That(failure!.Message, Does.Contain("not configured"));
        Assert.That(calls, Is.Zero);
    }

    [TestCase("sk_live_synthetic")]
    [TestCase("rk_live_synthetic")]
    [TestCase("rk_test_synthetic")]
    [TestCase("Sk_test_synthetic")]
    [TestCase("prefix_sk_test_synthetic")]
    [TestCase(" sk_test_synthetic")]
    public void Given_NonTestSecret_When_CheckoutRequested_Then_DemoGuardRejectsBeforeSdk(string key)
    {
        // Arrange
        var calls = 0;
        var service = new StripeCheckoutService(
            Options.Create(new StripeOptions { SecretKey = key }),
            NullLogger<StripeCheckoutService>.Instance,
            (_, _) => { calls++; return Task.FromResult(new Session()); });
        // Act
        var failure = Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateCheckoutSessionAsync(Draft(), Request()));
        // Assert
        Assert.That(failure!.Message, Does.Contain("must be a test key"));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public async Task Given_TestKey_When_CheckoutRequested_Then_ExactOptionsReachExternalAdapterOnce()
    {
        // Arrange
        var options = new StripeOptions { SecretKey = "sk_test_synthetic" };
        var expected = new Session { Id = "cs_test_synthetic", Url = "https://checkout.example.test/session" };
        var calls = 0;
        string? capturedKey = null;
        SessionCreateOptions? captured = null;
        var service = new StripeCheckoutService(
            Options.Create(options), NullLogger<StripeCheckoutService>.Instance,
            (key, request) => { calls++; capturedKey = key; captured = request; return Task.FromResult(expected); });
        // Act
        var actual = await service.CreateCheckoutSessionAsync(Draft(), Request());
        // Assert
        Assert.That(actual, Is.SameAs(expected));
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(capturedKey, Is.EqualTo(options.SecretKey));
        Assert.That(captured!.Mode, Is.EqualTo("payment"));
        Assert.That(captured.LineItems, Has.Count.EqualTo(1));
        Assert.That(captured.LineItems[0].Quantity, Is.EqualTo(2L));
        Assert.That(captured.LineItems[0].PriceData.Currency, Is.EqualTo("eur"));
        Assert.That(captured.LineItems[0].PriceData.UnitAmount, Is.EqualTo(1235L));
        Assert.That(captured.LineItems[0].PriceData.ProductData.Name, Is.EqualTo("Synthetic socks"));
        Assert.That(captured.CustomerEmail, Is.Null);
        Assert.That(captured.SuccessUrl, Is.EqualTo("https://shop.example.test" + options.SuccessReturnPath + "?session_id={CHECKOUT_SESSION_ID}"));
        Assert.That(captured.CancelUrl, Is.EqualTo("https://shop.example.test" + options.CancelReturnPath));
    }

    [Test]
    public void Given_ExternalFailure_When_TestCheckoutRequested_Then_OriginalFailurePropagates()
    {
        // Arrange
        var expected = new HttpRequestException("Synthetic SDK transport failure");
        var service = new StripeCheckoutService(
            Options.Create(new StripeOptions { SecretKey = "sk_test_synthetic" }),
            NullLogger<StripeCheckoutService>.Instance,
            (_, _) => Task.FromException<Session>(expected));
        // Act
        var actual = Assert.ThrowsAsync<HttpRequestException>(
            () => service.CreateCheckoutSessionAsync(Draft(), Request()));
        // Assert
        Assert.That(actual, Is.SameAs(expected));
    }

    [Test]
    public async Task Given_OptionsChangeDuringLogging_When_TestKeyWasValidated_Then_OnlyValidatedSnapshotReachesSdk()
    {
        // Arrange
        var options = new StripeOptions { SecretKey = "sk_test_synthetic" };
        var logger = new MutationLogger(() => options.SecretKey = "sk_live_synthetic");
        string? captured = null;
        var service = new StripeCheckoutService(Options.Create(options), logger,
            (key, _) => { captured = key; return Task.FromResult(new Session()); });
        // Act
        await service.CreateCheckoutSessionAsync(Draft(), Request());
        // Assert
        Assert.That(options.SecretKey, Is.EqualTo("sk_live_synthetic"));
        Assert.That(captured, Is.EqualTo("sk_test_synthetic"));
    }

    private sealed class MutationLogger(Action mutate) : Microsoft.Extensions.Logging.ILogger<StripeCheckoutService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) => mutate();
    }
}
