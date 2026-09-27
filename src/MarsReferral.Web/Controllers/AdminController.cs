using MarsReferral.Core;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarsReferral.Web.Controllers;
[Route("admin")]
[Authorize(AuthenticationSchemes = PortalSessions.Admin, Roles = "Operations")]
public class AdminController(ReferralService service) : PortalBase(service)
{
    protected override string PortalKey => "Admin";
    [HttpGet("")] public IActionResult Index() => Page("BackOffice");
    [AllowAnonymous, HttpGet("login")] public IActionResult Login() => View();
    [AllowAnonymous, HttpPost("login")] public async Task<IActionResult> Enter()
    {
        await PortalSessions.SignInAdmin(HttpContext); return RedirectToAction(nameof(Index));
    }
    [HttpPost("logout")] public async Task<IActionResult> Logout()
    {
        var customer = await HttpContext.AuthenticateAsync(PortalSessions.Customer);
        if (customer.Principal?.HasClaim("ShadowAdmin", "demo-admin") == true)
            await HttpContext.SignOutAsync(PortalSessions.Customer);
        await HttpContext.SignOutAsync(PortalSessions.Admin); return RedirectToAction(nameof(Login));
    }
    [HttpGet("customers")] public IActionResult Customers(string? q) => Page("Customers", q);
    [HttpGet("applications")] public IActionResult Applications(string? q, string? status) => Page("Applications", q, status);
    [HttpGet("rewards")] public IActionResult Rewards(string? status) => Page("Rewards", status: status);
    [HttpGet("inbox")] public IActionResult Inbox(int page = 1, int pageSize = 10) => Records("Inbox", page, pageSize);
    [HttpGet("activity")] public IActionResult Activity(int page = 1, int pageSize = 10) => Records("Activity", page, pageSize);
    [HttpPost("referral/attach")] public IActionResult Attach(int customerId, string? code) => Execute(() => Service.Attach(CurrentActor, customerId, code), "Customers");
    [HttpPost("referral/toggle")] public IActionResult ToggleCode(int customerId) => Execute(() => Service.ToggleCode(CurrentActor, customerId), "Customers");
    [HttpPost("applications/create")] public IActionResult CreateApplication(int customerId, string? purpose, decimal amount) => Execute(() => Service.CreateApplication(CurrentActor, customerId, purpose, amount), "Applications");
    [HttpPost("applications/approve")] public IActionResult ApproveAmount(int id, decimal amount) => Execute(() => Service.ApproveAmount(CurrentActor, id, amount), "Applications");
    [HttpPost("applications/transition")] public IActionResult Transition(int id, ApplicationStage stage) => Execute(() => {
        if (stage is not (ApplicationStage.Funded or ApplicationStage.Cancelled)) throw new RuleException("Use amount approval before confirming funding.");
        Service.Transition(CurrentActor, id, stage);
    }, "Applications");
    [HttpPost("rewards/review")] public IActionResult Review(int id, bool approve, string? reason) => Execute(() => Service.Review(CurrentActor, id, approve, reason), "Rewards");
    [HttpGet("enquiries")] public IActionResult Enquiries() => Page("Enquiries");
    [HttpPost("enquiries/contacted")] public IActionResult Contacted(string id) => Execute(()=>Service.MarkContacted(CurrentActor,id),"Enquiries");
    [HttpPost("rewards/pay")] public IActionResult Pay(PayInput input) {
        if(ModelState.IsValid) try {
            Service.Pay(CurrentActor,input.Id,input.Reference,input.Successful!.Value);
            TempData["Success"] = input.Successful.Value ? $"Payment simulation completed. Reward #{input.Id} is now Paid. No real funds were transferred." : $"Payment failure simulated for reward #{input.Id}. No paid balance was added. Select Retry payout to try again.";
            return RedirectToAction("Rewards");
        } catch(RuleException e) { ModelState.AddModelError("",e.Message); }
        ViewData["PayoutInput"] = input;
        return Page("Rewards");
    }
    [HttpPost("shadow/start")] public async Task<IActionResult> Shadow(int customerId)
    {
        if (!ModelState.IsValid) return BadRequest();
        try {
            Service.RecordShadow(CurrentActor, customerId, true);
            await PortalSessions.SignInCustomer(HttpContext, Service.Read().Customers.Single(x => x.Id == customerId), shadow: true);
            return RedirectToAction("Index", "Customer");
        } catch (RuleException e) { TempData["Error"] = e.Message; return RedirectToAction(nameof(Customers)); }
    }
    [HttpPost("shadow/stop")] public async Task<IActionResult> EndShadow()
    {
        var customer = await HttpContext.AuthenticateAsync(PortalSessions.Customer);
        if (customer.Principal?.HasClaim("ShadowAdmin", "demo-admin") == true) {
            var id = int.Parse(customer.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            Service.RecordShadow(CurrentActor, id, false);
            await HttpContext.SignOutAsync(PortalSessions.Customer);
        }
        return RedirectToAction(nameof(Customers));
    }
}