using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddScoped<PlatformHub.Mvc.Services.PlatformApiClient>();
var configuredApiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (!Uri.TryCreate(configuredApiBaseUrl, UriKind.Absolute, out var apiBaseAddress) ||
    (apiBaseAddress.Scheme != Uri.UriSchemeHttp && apiBaseAddress.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException(
        "Configuration 'Api:BaseUrl' must be an absolute HTTP/HTTPS URL, for example https://localhost:7201/.");
}

builder.Services.AddHttpClient("PlatformApi", client =>
{
    client.BaseAddress = apiBaseAddress;
    client.Timeout = TimeSpan.FromSeconds(30);
});
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
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.Run();
