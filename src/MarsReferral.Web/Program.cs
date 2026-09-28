using MarsReferral.Core;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider()
    .AddKeyManagementOptions(o => o.XmlRepository = new MemoryKeyRepository());
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSingleton<ReferralService>(_ => {
    var service = new ReferralService(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "referral-data.json"));
    service.RefreshPresentationDemo();
    service.SeedDemoEnquiries();
    return service;
});
builder.Services.AddAuthentication()
    .AddCookie(PortalSessions.Admin, o => PortalSessions.Configure(o, "Admin", "/admin/login"))
    .AddCookie(PortalSessions.Customer, o => {
        PortalSessions.Configure(o, "Customer", "/customer/login");
        o.Events.OnValidatePrincipal = async context => {
            var service = context.HttpContext.RequestServices.GetRequiredService<ReferralService>();
            var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var customer = service.Read().Customers.SingleOrDefault(c => c.Id.ToString() == id);
            var shadow = context.Principal?.HasClaim("ShadowAdmin", "demo-admin") == true;
            var valid = customer != null && (customer.OnboardingComplete || shadow);
            if (shadow)
                valid &= (await context.HttpContext.AuthenticateAsync(PortalSessions.Admin)).Succeeded;
            if (!valid) { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(PortalSessions.Customer); }
        };
    })
    .AddCookie(PortalSessions.Onboarding, o => PortalSessions.Configure(o, "Onboarding", "/join/register"));
builder.Services.AddAuthorization();
var app = builder.Build();
// Load and validate saved data before accepting requests.
_ = app.Services.GetRequiredService<ReferralService>();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseForwardedHeaders(new ForwardedHeadersOptions {
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
