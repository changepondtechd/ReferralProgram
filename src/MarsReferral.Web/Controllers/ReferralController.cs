using MarsReferral.Core;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
namespace MarsReferral.Web.Controllers;
[Route("referral")]
public class ReferralController(ReferralService service) : Controller {
    [HttpGet("")] public IActionResult Index(string? code,string? invitationId) => Landing(new EnquiryInput{Code=code ?? "",InvitationId=invitationId});
    private IActionResult Landing(EnquiryInput input) {
        ViewData["Portal"]="Referral";
        try {
            var owner=service.ReferralOwner(input.Code);
            if(input.InvitationId != null && !service.Read().Invitations.Any(x=>x.Id==input.InvitationId && x.ReferrerId==owner.Id)) throw new RuleException("This invitation link is invalid.");
            return View("Index",new ReferralLanding(owner,input));
        } catch(RuleException e) {Response.StatusCode=400;ViewData["ReferralError"]=e.Message;return View("Invalid");}
    }
    [HttpPost("enquire")] public async Task<IActionResult> Enquire(EnquiryInput input) {
        if(ModelState.IsValid) try {
            var session = await HttpContext.AuthenticateAsync(PortalSessions.Customer);
            int? authenticatedId = session.Succeeded && session.Principal?.HasClaim("ShadowAdmin","demo-admin") != true
                && int.TryParse(session.Principal?.FindFirstValue(ClaimTypes.NameIdentifier),out var id) ? id : null;
            var result=service.SubmitEnquiryAndRegister(input.Code,input.InvitationId,input.Name,input.Email,input.Phone,input.Interest,input.Consent,authenticatedId);
            if(result.AccountCreated) {
                await HttpContext.SignOutAsync(PortalSessions.Onboarding);
                await PortalSessions.SignInCustomer(HttpContext,result.Customer);
            }
            TempData["EnquiryReference"]="ENQ-"+result.Enquiry.Id[..8].ToUpperInvariant();
            TempData["EnquiryCustomerName"]=result.Customer.Name;
            TempData["EnquiryAccountMessage"]=result.AccountCreated?"Your demo account has been created automatically. You are signed in and ready to continue.":"Your enquiry is linked to your signed-in customer account. You are ready to continue.";
            TempData["EnquiryReferralMessage"]=result.ExistingReferralKept?"Your existing referral code remains attached to your profile and has not been replaced.":"Your friend's referral code is attached to your profile.";
            return RedirectToAction(nameof(Thanks));
        }catch(RuleException e){ModelState.AddModelError("",e.Message);}
        return Landing(input);
    }
    [HttpGet("thanks")] public IActionResult Thanks() {ViewData["Portal"]="Referral";return View();}
}
