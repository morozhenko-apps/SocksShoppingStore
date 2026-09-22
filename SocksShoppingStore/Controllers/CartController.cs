using Microsoft.AspNetCore.Mvc;
using SocksShoppingStore.Models;
using SocksShoppingStore.Helpers;
using SocksShoppingStore.Data; // Repository access

namespace SocksShoppingStore.Controllers
{
    public class CartController : Controller
    {
        private readonly SocksShoppingStore.Data.IProductRepository _repo;
        private readonly SocksShoppingStore.Services.TotalsCalculator? _totals;
        private readonly Microsoft.Extensions.Options.IOptions<SocksShoppingStore.Config.FeatureFlags>? _featureFlags;

        public CartController(
            SocksShoppingStore.Data.IProductRepository? repo = null,
            SocksShoppingStore.Services.TotalsCalculator? totals = null,
            Microsoft.Extensions.Options.IOptions<SocksShoppingStore.Config.FeatureFlags>? featureFlags = null)
        {
            _repo = repo ?? new SocksShoppingStore.Data.LegacyProductRepository();
            _totals = totals;
            _featureFlags = featureFlags;
        }

        public IActionResult Index()
        {
            var cart = HttpContext.Session.Get<ShoppingCart>("Cart") ?? new ShoppingCart();
            try
            {
                var ff = _featureFlags?.Value;
                var env = HttpContext.RequestServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                var enable = ff == null || !(ff.OnlyInDevelopment) || (env?.IsDevelopment() ?? false);
                var taxesOn = ff?.EnableTaxes ?? true;
                if (enable && taxesOn && _totals != null && cart.Items.Any())
                {
                    var items = cart.Items.Select(i => new OrderItem
                    {
                        ProductId = i.Sock.Id,
                        Name = i.Sock.Name,
                        UnitPrice = i.Sock.Price,
                        Quantity = i.Quantity
                    });
                    var (totals, _, _) = _totals.Compute(items, shippingCode: null, promoCode: null, estimated: true);
                    ViewBag.TaxLabel = totals.TaxLabel;
                    ViewBag.TaxRatePercent = totals.TaxRatePercent;
                    ViewBag.TaxAmount = totals.TaxAmount;
                    ViewBag.TotalInclTax = totals.TotalInclTax;
                }
            }
            catch { }
            return View(cart);
        }

        public IActionResult AddToCart(int id, string? returnUrl = null)
        {
            var cart = HttpContext.Session.Get<ShoppingCart>("Cart") ?? new ShoppingCart();
            var sock = _repo.GetSockById(id);
            if (sock != null)
            {
                cart.AddItem(sock);
                HttpContext.Session.Set("Cart", cart);
            }

            var accept = Request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                var item = cart.Items.FirstOrDefault(i => i.Sock.Id == id);
                return Json(new
                {
                    totalItems = cart.GetTotalItems(),
                    uniqueItems = cart.GetUniqueItemCount(),
                    totalSum = cart.GetTotalSum(),
                    item = item == null ? null : new { id = item.Sock.Id, quantity = item.Quantity, price = item.Sock.Price, subtotal = item.Sock.Price * item.Quantity }
                });
            }

            // Prefer explicit returnUrl if provided and is local
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            // Fallback to Referer when safe and not from Cart
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                // Convert absolute referer to a local path if it points to our host
                if (Uri.TryCreate(referer, UriKind.Absolute, out var refUri))
                {
                    var localPath = refUri.PathAndQuery;
                    if (Url.IsLocalUrl(localPath) && !localPath.StartsWith("/Cart", StringComparison.OrdinalIgnoreCase))
                    {
                        return LocalRedirect(localPath);
                    }
                }
            }

            // Final fallback: go home
            return RedirectToAction("Index", "Home");
        }

        public IActionResult RemoveFromCart(int id)
        {
            var cart = HttpContext.Session.Get<ShoppingCart>("Cart") ?? new ShoppingCart();
            cart.RemoveItem(id);
            HttpContext.Session.Set("Cart", cart);
            var accept = Request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                var item = cart.Items.FirstOrDefault(i => i.Sock.Id == id);
                return Json(new
                {
                    totalItems = cart.GetTotalItems(),
                    uniqueItems = cart.GetUniqueItemCount(),
                    totalSum = cart.GetTotalSum(),
                    item = item == null ? new { id = id, quantity = 0, price = 0m, subtotal = 0m } : new { id = item.Sock.Id, quantity = item.Quantity, price = item.Sock.Price, subtotal = item.Sock.Price * item.Quantity }
                });
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult SetQuantity(int id, int quantity)
        {
            var cart = HttpContext.Session.Get<ShoppingCart>("Cart") ?? new ShoppingCart();
            cart.SetQuantity(id, quantity);
            HttpContext.Session.Set("Cart", cart);
            var accept = Request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                var item = cart.Items.FirstOrDefault(i => i.Sock.Id == id);
                return Json(new
                {
                    totalItems = cart.GetTotalItems(),
                    uniqueItems = cart.GetUniqueItemCount(),
                    totalSum = cart.GetTotalSum(),
                    item = item == null ? new { id = id, quantity = 0, price = 0m, subtotal = 0m } : new { id = item.Sock.Id, quantity = item.Quantity, price = item.Sock.Price, subtotal = item.Sock.Price * item.Quantity }
                });
            }
            return RedirectToAction("Index");
        }

        public IActionResult DeleteItem(int id)
        {
            var cart = HttpContext.Session.Get<ShoppingCart>("Cart") ?? new ShoppingCart();
            cart.DeleteItem(id);
            HttpContext.Session.Set("Cart", cart);
            var accept = Request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    totalItems = cart.GetTotalItems(),
                    uniqueItems = cart.GetUniqueItemCount(),
                    totalSum = cart.GetTotalSum(),
                    item = new { id = id, quantity = 0, price = 0m, subtotal = 0m }
                });
            }
            return RedirectToAction("Index");
        }

        public IActionResult Clear()
        {
            var cart = new ShoppingCart();
            HttpContext.Session.Set("Cart", cart);
            var accept = Request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { totalItems = 0, uniqueItems = 0, totalSum = 0m });
            }
            return RedirectToAction("Index");
        }
    }
}
