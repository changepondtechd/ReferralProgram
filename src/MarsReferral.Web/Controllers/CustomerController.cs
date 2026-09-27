using MarsReferral.Core;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

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
    [HttpGet("referral/qr")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ReferralQr(bool download = false)
    {
        var snapshot = Service.Read();
        var customer = snapshot.Customers.SingleOrDefault(x => x.Id == CurrentActor.CustomerId);
        if (customer == null || !customer.OnboardingComplete) return NotFound();
        if (ReferralEligibility.UnavailableReason(customer, snapshot.Applications) is { } reason) return BadRequest(reason);
        var link = Url.Action("Index", "Referral", new { code = customer.Code }, Request.Scheme, Request.Host.Value);
        if (string.IsNullOrEmpty(link)) return StatusCode(StatusCodes.Status500InternalServerError);
        using var data = QRCodeGenerator.GenerateQrCode(link, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var image = qr.GetGraphic(8);
        return download ? File(image, "image/png", "referral-qr.png") : File(image, "image/png");
    }
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
    [HttpGet("messages")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Messages(int page = 1, int pageSize = 10)
    {
        var model = Build();
        var paging = new Pagination(model.Data.Invitations.Length, page, pageSize);
        var data = model.Data with { Invitations = model.Data.Invitations.OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id).Skip(paging.Skip).Take(paging.PageSize).ToArray() };
        return View("~/Views/Portal/Messages.cshtml", model with { Data = data, Pagination = paging });
    }
    [HttpGet("inbox")] public IActionResult Inbox(int page = 1, int pageSize = 10) => Records("Inbox", page, pageSize);
    [HttpGet("activity")] public IActionResult Activity(int page = 1, int pageSize = 10) => Records("Activity", page, pageSize);
    [HttpGet("heb")] public IActionResult Heb() => Page("Heb");
    [HttpPost("referral/create")] public IActionResult Refer(ReferInput input)
    {
        var asynchronous = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        if (ModelState.IsValid)
        {
            try {
                Service.Refer(CurrentActor, input.Name, input.Email, input.Phone);
                if (asynchronous) return Json(new { success = true });
                TempData["DirectReferralShared"] = true;
                return RedirectToAction(nameof(Index));
            } catch (RuleException ex) { ModelState.AddModelError("", ex.Message); }
        }
        if (asynchronous) return BadRequest(new { errors = ModelState.Values.SelectMany(x => x.Errors)
            .Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Check the submitted referral details." : x.ErrorMessage).ToArray() });
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