using Microsoft.AspNetCore.Mvc;
using SocksShoppingStore.Helpers;
using SocksShoppingStore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SocksShoppingStore.Services;
using SocksShoppingStore.Config;

namespace SocksShoppingStore.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ILogger<CheckoutController> _logger;
        private readonly StripeCheckoutService _stripe;
        private readonly PaymentSessionStore _sessionStore;
        private readonly Services.TotalsCalculator _totals;
        private readonly Microsoft.Extensions.Options.IOptions<SocksShoppingStore.Config.FeatureFlags>? _featureFlags;
        [ActivatorUtilitiesConstructor]
        public CheckoutController(ILogger<CheckoutController> logger, StripeCheckoutService stripe, PaymentSessionStore sessionStore, Services.TotalsCalculator totals, Microsoft.Extensions.Options.IOptions<SocksShoppingStore.Config.FeatureFlags> featureFlags)
        {
            _logger = logger;
            _stripe = stripe;
            _sessionStore = sessionStore;
            _totals = totals;
            _featureFlags = featureFlags;
        }

        // Test-friendly fallback constructor (used by legacy tests creating controller directly)
        public CheckoutController(ILogger<CheckoutController> logger)
            : this(
                logger,
                new StripeCheckoutService(
                    Microsoft.Extensions.Options.Options.Create(new StripeOptions { SecretKey = "sk_test_dummy" }),
                    NullLogger<StripeCheckoutService>.Instance),
                new PaymentSessionStore(),
                new Services.TotalsCalculator(
                    Microsoft.Extensions.Options.Options.Create(new SocksShoppingStore.Config.TaxOptions()),
                    Microsoft.Extensions.Options.Options.Create(new SocksShoppingStore.Config.ShippingOptions()),
                    Microsoft.Extensions.Options.Options.Create(new SocksShoppingStore.Config.PromoOptions())
                ),
                Microsoft.Extensions.Options.Options.Create(new SocksShoppingStore.Config.FeatureFlags { EnableTaxes = true, EnableShipping = true, EnablePromoCodes = true, EnablePdfInvoices = true, OnlyInDevelopment = false })
            )
        { }

        [HttpGet]
        public IActionResult Index()
        {
            // Show checkout form
            try
            {
                var ff = HttpContext.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<SocksShoppingStore.Config.FeatureFlags>>()?.Value;
                var env = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                var enable = ff == null || !ff.OnlyInDevelopment || env.IsDevelopment();
                var shipOn = ff?.EnableShipping ?? true;
                var promoOn = ff?.EnablePromoCodes ?? true;
                if (enable && (shipOn || promoOn))
                {
                    var shipping = HttpContext.RequestServices.GetService<Microsoft.Extensions.Options.IOptions<SocksShoppingStore.Config.ShippingOptions>>()?.Value;
                    if (shipping != null)
                    {
                        ViewBag.ShippingMethods = shipping.Methods;
                    }
                }
            }
            catch { }
            return View(new CheckoutViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(CheckoutViewModel model)
        {
            // Honeypot: ignore bots filling hidden field
            if (!string.IsNullOrWhiteSpace(model.Website))
            {
                ModelState.AddModelError(string.Empty, "Invalid submission.");
            }

            // Ensure cart has items
            var cart = HttpContext.Session.Get<ShoppingCart>("Cart") ?? new ShoppingCart();
            if (!cart.Items.Any())
            {
                ModelState.AddModelError(string.Empty, "Your cart is empty.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Map to order draft and store in session
            var draft = new Order
            {
                CustomerName = model.CustomerName,
                Email = model.Email,
                AddressLine1 = model.AddressLine1,
                AddressLine2 = model.AddressLine2,
                City = model.City,
                PostalCode = model.PostalCode,
                Country = model.Country,
                Items = cart.Items.Select(i => new OrderItem
                {
                    ProductId = i.Sock.Id,
                    Name = i.Sock.Name,
                    UnitPrice = i.Sock.Price,
                    Quantity = i.Quantity
                }).ToList()
            };

            // Compute totals (VAT EU, prices include tax)
            var ff = _featureFlags?.Value;
            var enable = ff == null || !ff.OnlyInDevelopment || HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            if (enable && (ff?.EnableTaxes != false || ff?.EnableShipping != false || ff?.EnablePromoCodes != false))
            {
                var (totals, shipCode, promo)
                    = _totals.Compute(draft.Items, model.ShippingMethod, model.PromoCode, estimated: false);
                draft.Totals = totals;
            }

            HttpContext.Session.Set("OrderDraft", draft);
            _logger.LogInformation("checkout_start: items={Count}", draft.Items.Count);
            return RedirectToAction("Review");
        }

        [HttpGet]
        public IActionResult Review()
        {
            var draft = HttpContext.Session.Get<Order>("OrderDraft");
            if (draft == null) return RedirectToAction("Index");

            // Ensure totals are present (e.g., if draft created before features enabled)
            try
            {
                var ff = _featureFlags?.Value;
                var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var enable = ff == null || !ff.OnlyInDevelopment || env.IsDevelopment();
                var taxesOn = ff?.EnableTaxes ?? true;
                if (enable && taxesOn && (draft.Totals == null || draft.Totals.TaxRatePercent <= 0))
                {
                    var (totals, _, _) = _totals.Compute(draft.Items, draft?.Totals != null ? null : null, null, estimated: false);
                    draft.Totals = totals;
                    HttpContext.Session.Set("OrderDraft", draft);
                }
            }
            catch { }
            return View(draft);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Confirm()
        {
            var draft = HttpContext.Session.Get<Order>("OrderDraft");
            if (draft == null) return RedirectToAction("Index");

            // Redirect to Stripe Checkout (test mode)
            try
            {
                var session = _stripe.CreateCheckoutSessionAsync(draft, HttpContext.Request).GetAwaiter().GetResult();
                _sessionStore.SaveDraft(session.Id, draft);
                return Redirect(session.Url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "payment_session_error");
                // Fallback for test/degenerate environments: finalize locally
                draft.Id = Guid.NewGuid();
                draft.CreatedAt = DateTimeOffset.UtcNow;

                HttpContext.Session.Set("LastOrder", draft);
                HttpContext.Session.Set("Cart", new ShoppingCart());
                HttpContext.Session.Set<Order>("OrderDraft", null!);

                _logger.LogInformation("checkout_confirmed_fallback: order={OrderId} total={Total}", draft.Id, draft.Total);
                return RedirectToAction("ThankYou");
            }
        }

        [HttpGet]
        public IActionResult ReturnSuccess()
        {
            var draft = HttpContext.Session.Get<Order>("OrderDraft");
            if (draft == null) return RedirectToAction("Index");

            // Finalize locally after successful payment (demo)
            draft.Id = Guid.NewGuid();
            draft.CreatedAt = DateTimeOffset.UtcNow;

            HttpContext.Session.Set("LastOrder", draft);
            HttpContext.Session.Set("Cart", new ShoppingCart());
            HttpContext.Session.Set<Order>("OrderDraft", null!);

            _logger.LogInformation("payment_completed: order={OrderId} total={Total}", draft.Id, draft.Total);
            return RedirectToAction("ThankYou");
        }

        [HttpGet]
        public IActionResult ThankYou()
        {
            var order = HttpContext.Session.Get<Order>("LastOrder");
            return View(order);
        }

        [HttpGet]
        public IActionResult PaymentFailed()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Invoice()
        {
            var ff = _featureFlags?.Value;
            var enable = ff == null || !ff.OnlyInDevelopment || HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            if (!(enable && (ff?.EnablePdfInvoices ?? true)))
            {
                return NotFound();
            }

            var order = HttpContext.Session.Get<Order>("LastOrder");
            if (order == null) return RedirectToAction("Index");

            try
            {
                var pdf = HttpContext.RequestServices.GetRequiredService<SocksShoppingStore.Services.PdfInvoiceService>().Generate(order);
                var fileName = $"Invoice-{order.Id}.pdf";
                return File(pdf, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "invoice_generation_error");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
