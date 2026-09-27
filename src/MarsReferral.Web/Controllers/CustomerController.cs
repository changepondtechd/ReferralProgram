using MarsReferral.Core;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarsReferral.Web.Controllers;
[Route("customer")]
[Authorize(AuthenticationSchemes = PortalSessions.Customer, Roles = "Customer")]
public class CustomerController(ReferralService service) : PortalBase(service)
{
    protected override string PortalKey => "Customer";
    [HttpGet("enquiries")] public IActionResult Enquiries() => Page("CustomerEnquiries");
    [HttpGet("sharing")] public IActionResult Sharing(string? id) => SharingPage(id:id);
    [HttpPost("sharing/send")] public IActionResult Send(ShareInput input) => SendSharing(input);
    [HttpPost("sharing/deliver")] public IActionResult Deliver(string id) => DeliverSharing(id);
    [HttpGet("")] public IActionResult Index() => Page("Index");
    [AllowAnonymous, HttpGet("login")] public IActionResult Login() => View(Service.Read().Customers);
    [AllowAnonymous, HttpPost("login")] public async Task<IActionResult> Enter(int customerId)
    {
        var customer = Service.Read().Customers.SingleOrDefault(x => x.Id == customerId);
        if (customer == null) { TempData["Error"] = "Choose an existing demo customer."; return RedirectToAction(nameof(Login)); }
        if (!customer.OnboardingComplete) {
            await PortalSessions.SignInOnboarding(HttpContext, customer);
            return RedirectToAction("Welcome", "Onboarding");
        }
        await PortalSessions.SignInCustomer(HttpContext, customer);
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("logout")] public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(PortalSessions.Customer); return RedirectToAction(nameof(Login));
    }
    [HttpGet("applications")] public IActionResult Applications(string? q, string? status) => Page("Applications", q, status);
    [HttpGet("rewards")] public IActionResult Rewards(string? status) => Page("Rewards", status: status);
    [HttpGet("inbox")] public IActionResult Inbox(int page = 1, int pageSize = 10) => Records("Inbox", page, pageSize);
    [HttpGet("activity")] public IActionResult Activity(int page = 1, int pageSize = 10) => Records("Activity", page, pageSize);
    [HttpGet("heb")] public IActionResult Heb() => Page("Heb");
    [HttpPost("referral/create")] public IActionResult Refer(ReferInput input)
    {
        if (ModelState.IsValid)
        {
            try {
                var customer = Service.Refer(CurrentActor, input.Name, input.Email, input.Phone);
                TempData["Success"] = $"{customer.Name}'s demo account is ready! They can select their name on Customer sign-in. Your referral code is attached. No email or SMS has been sent.";
                return RedirectToAction(nameof(Index));
            } catch (RuleException ex) { ModelState.AddModelError("", ex.Message); }
        }
        ViewData["ReferInput"] = input;
        ViewData["OpenReferral"] = true;
        return Page("Index");
    }
    [HttpGet("guide")] public IActionResult Guide() => Page("Guide");
    [HttpPost("referral/attach")] public IActionResult Attach(string? code) => Execute(() => Service.Attach(CurrentActor, CurrentActor.CustomerId, code), "Index");
    [HttpPost("applications/create")] public IActionResult CreateApplication(string? purpose, decimal amount) =>
        Execute(() => Service.CreateApplication(CurrentActor, CurrentActor.CustomerId, purpose, amount), "Applications");
    // This endpoint authenticates the shadow customer token for CSRF, then checks
    // the independent admin session before clearing the shadow session.
    [HttpPost("shadow/return")] public async Task<IActionResult> ReturnToAdmin()
    {
        if (!User.HasClaim("ShadowAdmin", "demo-admin") || !(await HttpContext.AuthenticateAsync(PortalSessions.Admin)).Succeeded) return Forbid(PortalSessions.Customer);
        Service.RecordShadow(new(IsOperations: true), CurrentActor.CustomerId, false);
        await HttpContext.SignOutAsync(PortalSessions.Customer);
        return RedirectToAction("Customers", "Admin");
    }
}