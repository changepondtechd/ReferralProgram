using System.Security.Claims;
using MarsReferral.Core;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MarsReferral.Web.Controllers;
[Route("join")]
[Authorize(AuthenticationSchemes = PortalSessions.Onboarding)]
public class OnboardingController(ReferralService service) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context) { ViewData["Portal"] = "Onboarding"; base.OnActionExecuting(context); }
    [AllowAnonymous, HttpGet("")] public IActionResult Index(string? code) => RedirectToAction(nameof(Register), new {code});
    [AllowAnonymous, HttpGet("register")] public IActionResult Register(string? code) => View("~/Views/Account/Register.cshtml", new RegisterInput { ReferralCode = code });
    [AllowAnonymous, HttpPost("register")] public async Task<IActionResult> Register(RegisterInput input)
    {
        if (!ModelState.IsValid) return View("~/Views/Account/Register.cshtml", input);
        try {
            var user = service.Register(input.Name, input.Email, input.ReferralCode, onboardingRequired: true);
            await PortalSessions.SignInOnboarding(HttpContext, user);
            return RedirectToAction(nameof(Welcome));
        } catch (RuleException e) { ModelState.AddModelError("", e.Message); return View("~/Views/Account/Register.cshtml", input); }
    }
    private Customer? Current => service.Read().Customers.SingleOrDefault(x => x.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier));
    [HttpGet("welcome")] public IActionResult Welcome()
    {
        var user = Current; if (user == null) return RedirectToAction(nameof(Register));
        return View(user);
    }
    [HttpPost("complete")] public async Task<IActionResult> Complete(bool understood)
    {
        var user = Current; if (user == null) return RedirectToAction(nameof(Register));
        try {
            if (!ModelState.IsValid) throw new RuleException("Confirm the referral introduction to continue.");
            service.CompleteOnboarding(new(user.Id), user.Id, understood);
            await HttpContext.SignOutAsync(PortalSessions.Onboarding);
            await PortalSessions.SignInCustomer(HttpContext, user);
            TempData["Success"] = "Onboarding complete. Welcome to your customer portal.";
            return RedirectToAction("Index", "Customer");
        } catch (RuleException e) { TempData["Error"] = e.Message; return RedirectToAction(nameof(Welcome)); }
    }
}