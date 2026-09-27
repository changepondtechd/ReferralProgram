using System.Security.Claims;
using MarsReferral.Core;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MarsReferral.Web.Controllers;
public abstract class PortalBase(ReferralService service) : Controller
{
    protected readonly ReferralService Service = service;
    protected abstract string PortalKey { get; }
    protected Actor CurrentActor => PortalKey == "Admin" ? new(IsOperations: true)
        : new(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), IsShadow: User.HasClaim("ShadowAdmin", "demo-admin"));
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ViewData["Portal"] = PortalKey;
        base.OnActionExecuting(context);
    }
    protected PortalModel Build(string? q = null, string? status = null)
    {
        var data = Service.Read(); var actor = CurrentActor;
        if (!actor.IsOperations) data = data with {
            Invitations = data.Invitations.Where(x => x.ReferrerId == actor.CustomerId).ToArray(),
            Enquiries = data.Enquiries.Where(x => x.ReferrerId == actor.CustomerId ||
                string.Equals(x.Email, data.Customers.Single(c => c.Id == actor.CustomerId).Email, StringComparison.OrdinalIgnoreCase))
                .Select(x => string.Equals(x.Email, data.Customers.Single(c => c.Id == actor.CustomerId).Email, StringComparison.OrdinalIgnoreCase)
                    ? x : x with { Email = "", Phone = "", Interest = "", InvitationId = null }).ToArray(),
            Applications = data.Applications.Where(x => x.CustomerId == actor.CustomerId).ToArray(),
            Rewards = data.Rewards.Where(x => x.BeneficiaryId == actor.CustomerId).ToArray(),
            Messages = data.Messages.Where(x => x.CustomerId == actor.CustomerId).ToArray(), Audit = data.Audit.Where(x => x.CustomerId == actor.CustomerId).ToArray()
        };
        return new(data, actor, q?.Trim() ?? "", status ?? "");
    }
    protected IActionResult Page(string view, string? q = null, string? status = null) =>
        View("~/Views/Portal/" + view + ".cshtml", Build(q, status));
    protected IActionResult Records(string view, int page, int pageSize)
    {
        var model = Build();
        var paging = new Pagination(view == "Inbox" ? model.Data.Messages.Length : model.Data.Audit.Length, page, pageSize);
        var data = view == "Inbox"
            ? model.Data with { Messages = model.Data.Messages.OrderByDescending(x => x.Id).Skip(paging.Skip).Take(paging.PageSize).ToArray() }
            : model.Data with { Audit = model.Data.Audit.OrderByDescending(x => x.At).Skip(paging.Skip).Take(paging.PageSize).ToArray() };
        return View("~/Views/Portal/" + view + ".cshtml", model with { Data = data, Pagination = paging });
    }
    protected IActionResult SharingPage(ShareInput? input = null, string? id = null) {
        var portal = Build();
        var active = portal.Data.Invitations.SingleOrDefault(x => x.Id == id);
        if (id != null && active == null) return NotFound();
        if(input == null && active != null) input = new ShareInput {CustomerId=active.ReferrerId,Recipient=active.Recipient,Channel=active.Channel,Body=active.Body,Contact=active.Contact};
        input ??= new ShareInput { CustomerId = portal.IsOperations ? portal.Data.Customers.FirstOrDefault(x=>x.CodeActive && x.OnboardingComplete)?.Id ?? 0 : CurrentActor.CustomerId };
        return View("~/Views/Portal/Sharing.cshtml",new SharingModel(portal,input,active));
    }
    protected IActionResult SendSharing(ShareInput input) {
        if(!CurrentActor.IsOperations) input.CustomerId = CurrentActor.CustomerId;
        if(ModelState.IsValid) try {
            var invite = Service.SendInvitation(CurrentActor,input.CustomerId,input.Recipient,input.Channel,input.Body,input.Contact);
            TempData["Success"] = $"Referral link sent to {invite.Recipient} via {invite.Channel} (demo). No external message was sent.";
            return RedirectToAction("Sharing",new {id=invite.Id});
        } catch(RuleException e) { ModelState.AddModelError("",e.Message); }
        return SharingPage(input);
    }
    protected IActionResult DeliverSharing(string id) {
        try { Service.DeliverInvitation(CurrentActor,id); TempData["Success"]="Delivered in the demo. The recipient preview below shows the invitation and referral link."; }
        catch(RuleException e) {TempData["Error"]=e.Message;}
        return RedirectToAction("Sharing",new{id});
    }
    protected IActionResult Execute(Action action, string page)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "The submitted values are invalid. Check the form and try again."; return RedirectToAction(page); }
        try { action(); TempData["Success"] = "Saved successfully. Your dashboard is up to date."; }
        catch (RuleException e) { TempData["Error"] = e.Message; }
        return RedirectToAction(page);
    }
}