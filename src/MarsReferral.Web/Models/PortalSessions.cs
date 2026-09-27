using System.Security.Claims;
using MarsReferral.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
namespace MarsReferral.Web.Models;

public static class PortalSessions
{
    public const string Admin = "MarsAdmin";
    public const string Customer = "MarsCustomer";
    public const string Onboarding = "MarsOnboarding";
    public static void Configure(CookieAuthenticationOptions options, string name, string login)
    {
        options.Cookie.Name = "MarsReferral." + name;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = login;
        options.AccessDeniedPath = "/denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    }
    public static Task SignInAdmin(HttpContext context) => context.SignInAsync(Admin,
        Principal(Admin, "demo-admin", "Demo Admin", "Operations"));
    public static Task SignInCustomer(HttpContext context, Customer customer, bool shadow = false) =>
        context.SignInAsync(Customer, Principal(Customer, customer.Id.ToString(), customer.Name, "Customer", shadow));
    public static Task SignInOnboarding(HttpContext context, Customer customer) =>
        context.SignInAsync(Onboarding, Principal(Onboarding, customer.Id.ToString(), customer.Name, "Onboarding"));
    private static ClaimsPrincipal Principal(string scheme, string id, string name, string role, bool shadow = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id), new(ClaimTypes.Name, name), new(ClaimTypes.Role, role) };
        if (shadow) claims.Add(new("ShadowAdmin", "demo-admin"));
        return new(new ClaimsIdentity(claims, scheme));
    }
}