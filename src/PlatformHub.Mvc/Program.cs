using Microsoft.AspNetCore.Authentication.Cookies;
using PlatformHub.Mvc.Services;

var builder = WebApplication.CreateBuilder(args);

var configuredApiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (!Uri.TryCreate(configuredApiBaseUrl, UriKind.Absolute, out var apiBaseAddress) ||
    (apiBaseAddress.Scheme != Uri.UriSchemeHttp && apiBaseAddress.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException(
        "Configuration 'Api:BaseUrl' must be an absolute HTTP/HTTPS URL, for example https://localhost:7201/.");
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PlatformApiClient>();
builder.Services.AddTransient<PlatformApiResilienceHandler>();
builder.Services.AddTransient<ActiveTenantScopeHeaderHandler>();
builder.Services.AddHttpClient("PlatformApi", client =>
{
    client.BaseAddress = apiBaseAddress;
    // Fail quickly enough to render the MVC Error Center instead of leaving a page hanging.
    client.Timeout = TimeSpan.FromSeconds(12);
})
.AddHttpMessageHandler<PlatformApiResilienceHandler>()
.AddHttpMessageHandler<ActiveTenantScopeHeaderHandler>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.Cookie.Name = "__Host-PlatformHub.Mvc";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Establish a safe correlation ID before exception/status handlers so the same reference
// is shown to the user and included in MVC/API logs.
app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var isSafe = !string.IsNullOrWhiteSpace(supplied)
        && supplied.Length <= 64
        && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    var correlationId = isSafe ? supplied! : context.TraceIdentifier;

    context.TraceIdentifier = correlationId;
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("PlatformHub.Requests");
    using (logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["RequestMethod"] = context.Request.Method,
        ["RequestPath"] = context.Request.Path.Value ?? "/"
    }))
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled request exception. CorrelationId={CorrelationId}", correlationId);
            throw;
        }
    }
});

app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");

// API token expiry is an authentication event, not an application error.
// Redirect to the appropriate panel login only when refresh has genuinely failed.
app.Use(async (context, next) =>
{
    await next();
    if (context.Items["PlatformHub.SessionExpired"] is true && !context.Response.HasStarted)
    {
        var isPlatformAdmin = context.Request.Path.StartsWithSegments("/PlatformAdmin")
            || context.User.FindFirst("ph_platform_admin")?.Value == "true";
        context.Response.Clear();
        context.Response.Redirect(isPlatformAdmin ? "/PlatformAdmin/Account/Login" : "/Account/Login");
    }
});

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.Run();
