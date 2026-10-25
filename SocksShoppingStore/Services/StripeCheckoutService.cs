using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocksShoppingStore.Models;
using SocksShoppingStore.Config;
using Stripe;
using Stripe.Checkout;

namespace SocksShoppingStore.Services
{
    public class StripeCheckoutService
    {
        private readonly StripeOptions _options;
        private readonly ILogger<StripeCheckoutService> _logger;
        private readonly Func<string, SessionCreateOptions, Task<Session>> _createSession;

        public StripeCheckoutService(
            IOptions<StripeOptions> options,
            ILogger<StripeCheckoutService> logger,
            Func<string, SessionCreateOptions, Task<Session>>? createSession = null)
        {
            _options = options.Value;
            _logger = logger;
            _createSession = createSession ?? ((key, createOptions) =>
                new SessionService(new StripeClient(key)).CreateAsync(createOptions));
        }

        public async Task<Session> CreateCheckoutSessionAsync(Order draft, HttpRequest request)
        {
            var secretKey = _options.SecretKey;
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("Stripe SecretKey is not configured.");
            }

            if (!secretKey.StartsWith("sk_test_", StringComparison.Ordinal))
            {
                // Hard guard to avoid accidental live charges in dev/staging
                throw new InvalidOperationException("Stripe SecretKey must be a test key (sk_test_*) in this environment.");
            }

            var lineItems = draft.Items.Select(i => new SessionLineItemOptions
            {
                Quantity = i.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "eur",
                    UnitAmount = ToCents(i.UnitPrice),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = i.Name
                    }
                }
            }).ToList();

            var host = request.Host.HasValue ? request.Host.Value : "localhost";
            var scheme = request.Scheme ?? "https";
            var baseUrl = $"{scheme}://{host}";
            var successUrl = baseUrl + _options.SuccessReturnPath + "?session_id={CHECKOUT_SESSION_ID}";
            var cancelUrl = baseUrl + _options.CancelReturnPath;

            var createOptions = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                LineItems = lineItems,
                CustomerEmail = string.IsNullOrWhiteSpace(draft.Email) ? null : draft.Email,
                // Stripe will auto-detect locale if not set; we can map culture later
                Locale = null,
            };

            _logger.LogInformation("payment_session_creating: items={Count} total={Total}", draft.Items.Count, draft.Total);
            var session = await _createSession(secretKey, createOptions);
            _logger.LogInformation("payment_session_created: id={Id} url={Url}", session.Id, session.Url);
            return session;
        }

        private static long ToCents(decimal amount)
        {
            return (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        }
    }
}
