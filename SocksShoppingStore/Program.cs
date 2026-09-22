using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using SocksShoppingStore.Middleware;
using Microsoft.AspNetCore.StaticFiles;
using System.Text.Json;
using SocksShoppingStore.Controllers;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using SocksShoppingStore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.CookiePolicy;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources"); // UI localization scaffolding
builder.Services.AddControllersWithViews().AddViewLocalization().AddDataAnnotationsLocalization();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Время жизни сессии
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Payments
builder.Services.AddScoped<SocksShoppingStore.Services.StripeCheckoutService>();
builder.Services.AddSingleton<SocksShoppingStore.Services.PaymentSessionStore>();
builder.Services.AddSingleton<SocksShoppingStore.Services.TotalsCalculator>();
builder.Services.AddSingleton<SocksShoppingStore.Services.PdfInvoiceService>();

// Default culture for numbers (we will format prices explicitly as EUR)
var defaultCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

// Allow Unicode (incl. Cyrillic) to render unescaped in HTML output
builder.Services.AddWebEncoders(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic, UnicodeRanges.Latin1Supplement, UnicodeRanges.GeneralPunctuation);
});

// Bind options from configuration
builder.Services.Configure<FreeTierOptions>(builder.Configuration.GetSection("FreeTier"));
builder.Services.Configure<ConcurrencyOptions>(builder.Configuration.GetSection("Concurrency"));
builder.Services.Configure<LegalOptions>(builder.Configuration.GetSection("Legal"));
builder.Services.Configure<SocksShoppingStore.Config.StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<SocksShoppingStore.Config.TaxOptions>(builder.Configuration.GetSection("Tax"));
builder.Services.Configure<SocksShoppingStore.Config.ShippingOptions>(builder.Configuration.GetSection("Shipping"));
builder.Services.Configure<SocksShoppingStore.Config.PromoOptions>(builder.Configuration.GetSection("Promotions"));
builder.Services.Configure<SocksShoppingStore.Config.FeatureFlags>(builder.Configuration.GetSection("Features"));

// Repository provider (InMemory default, optional JSON persistence)
var repoProvider = builder.Configuration.GetSection("Repository:Provider").Get<string>() ?? "InMemory";
if (string.Equals(repoProvider, "Json", StringComparison.OrdinalIgnoreCase))
{
    var relPath = builder.Configuration.GetSection("Repository:Json:Path").Get<string>() ?? "App_Data/products.json";
    var absPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(builder.Environment.ContentRootPath, relPath);
    builder.Services.AddSingleton<SocksShoppingStore.Data.IProductRepository>(sp => new SocksShoppingStore.Data.JsonProductRepository(absPath));
}
else
{
    builder.Services.AddSingleton<SocksShoppingStore.Data.IProductRepository, SocksShoppingStore.Data.LegacyProductRepository>();
}

var rateOptions = builder.Configuration.GetSection("RateLimiting").Get<RateOptions>() ?? new RateOptions();

// Rate limiting policies (safe defaults for free tier)
builder.Services.AddRateLimiter(options =>
{
    // Global per-IP window limiter
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: key => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateOptions.GlobalPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // API per-IP window limiter
    options.AddPolicy<string>("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: key => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateOptions.ApiPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Friendly 429 response
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var accept = context.HttpContext.Request.Headers["Accept"].ToString();
        if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync("{\"error\":\"Too Many Requests\",\"message\":\"Rate limit exceeded. Please retry later.\"}", token);
        }
        else
        {
            context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
            await context.HttpContext.Response.WriteAsync("<h1>Too Many Requests</h1><p>Rate limit exceeded. Please retry later.</p>", token);
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Allow disabling HTTPS redirect via config for CI runners (where HTTPS may be unavailable)
var httpsRedirectEnabled = builder.Configuration.GetValue<bool?>("HttpsRedirect:Enabled");
if (httpsRedirectEnabled != false)
{
    app.UseHttpsRedirection();
}
// Static files with cache headers
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static assets for 7 days to reduce traffic
        const int days = 7;
        ctx.Context.Response.Headers["Cache-Control"] = $"public, max-age={days * 24 * 3600}";
    }
});

app.UseRouting();

// Request localization: support EN/RU via cookie and query string
var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ru") };
var locOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
locOptions.AddInitialRequestCultureProvider(new QueryStringRequestCultureProvider());
locOptions.AddInitialRequestCultureProvider(new CookieRequestCultureProvider());
app.UseRequestLocalization(locOptions);

// Free-tier guard to avoid accidental costs (can block on Azure)
app.UseMiddleware<FreeTierGuardMiddleware>();

// Security headers (CSP, frame, sniffing, referrer, permissions)
app.UseMiddleware<SecurityHeadersMiddleware>();

// Cookie policy (essential cookies only; secure & samesite sane defaults)
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Lax,
    HttpOnly = HttpOnlyPolicy.Always,
    Secure = app.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always
});

// Global concurrency limiter (fast-fail over 10 concurrent requests)
app.UseMiddleware<ConcurrencyLimiterMiddleware>();

// Apply default rate limiter
app.UseRateLimiter();

app.UseAuthorization();
app.UseSession();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Lightweight health endpoint (allowed in FreeTier mode) with short cache
app.MapGet("/healthz", (HttpContext ctx) => Results.Ok("OK"));

// Robots.txt to discourage crawling on free tier (cache short)
app.MapGet("/robots.txt", (HttpContext ctx) => Results.Text("User-agent: *\nDisallow: /\n", "text/plain"))
   .WithMetadata(new ResponseCacheAttribute { Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false });

// Metrics storage and endpoint
var metrics = new RequestMetrics(
    builder.Configuration.GetSection("Status:LatencyWindowSize").Get<int?>() ?? 200);

app.Use(async (ctx, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try { await next(); }
    finally
    {
        sw.Stop();
        metrics.Record(ctx.Response.StatusCode, sw.Elapsed.TotalMilliseconds);
    }
});

app.MapGet("/_status", (HttpContext ctx) =>
{
    // Allowlist by IP
    var allow = builder.Configuration.GetSection("Status:AllowIPs").Get<string[]>() ?? new[] {"127.0.0.1","::1"};
    var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
    if (!allow.Contains(remoteIp)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var snapshot = metrics.Snapshot();
    return Results.Json(snapshot, new JsonSerializerOptions { WriteIndented = true });
});

app.Run();

public partial class Program { }
