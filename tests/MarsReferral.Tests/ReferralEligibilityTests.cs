using System.Security.Claims;
using System.Text.Json;
using MarsReferral.Core;
using MarsReferral.Web.Controllers;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal static class ReferralEligibilityTests
{
    public static void Run()
    {
        static void Check(bool value) { if (!value) throw new Exception("Referral eligibility assertion failed"); }
        static void Reject(Action action)
        {
            try { action(); }
            catch (RuleException) { return; }
            throw new Exception("Expected eligibility validation");
        }
        using var service = new ReferralService();
        var ops = new Actor(IsOperations: true);
        var owner = service.Register("Unfunded Owner", "eligibility-owner@example.com", null);
        var other = service.Register("Funded Other", "eligibility-other@example.com", null);
        var recipient = service.Register("Existing Recipient", "eligibility-recipient@example.com", null);
        var actor = new Actor(owner.Id);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()) }, "test"));
        var controller = new CustomerController(service) { ControllerContext = new ControllerContext { HttpContext = context } };
        var landing = new ReferralController(service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        void AssertBlocked()
        {
            var before = JsonSerializer.Serialize(service.Read());
            Reject(() => service.SendInvitation(actor, owner.Id, "Social Friend", "WhatsApp", "Please explore this referral link.", "4165550123"));
            Reject(() => service.SendInvitation(ops, owner.Id, "Social Friend", "LinkedIn", "Please explore this referral link.", "social-friend"));
            Reject(() => service.Refer(actor, "Direct Friend", "eligibility-direct@example.com", "4165550123"));
            Reject(() => service.Refer(new(owner.Id, IsShadow: true), "Shadow Friend", "eligibility-shadow@example.com", "4165550123"));
            Reject(() => service.ReferralOwner(owner.Code));
            Reject(() => service.Register("New Friend", "eligibility-new@example.com", owner.Code));
            Reject(() => service.Attach(new(recipient.Id), recipient.Id, owner.Code));
            Reject(() => service.SubmitEnquiryAndRegister(owner.Code, null, "Enquiry Friend", "eligibility-enquiry@example.com", "4165550123", "General enquiry", true));
            Check(controller.ReferralQr() is BadRequestObjectResult or NotFoundResult);
            Check(controller.ReferralQr(true) is BadRequestObjectResult or NotFoundResult);
            Check(landing.Index(owner.Code, null) is ViewResult invalid && invalid.ViewName == "Invalid");
            var portal = (PortalModel)((ViewResult)controller.Index()).Model!;
            Check(!portal.CanShare(portal.Current!) && portal.SharingUnavailableReason != null);
            Check(JsonSerializer.Serialize(service.Read()) == before);
        }
        Check(ReferralEligibility.CanShare(owner, service.Read().Applications));
        Check(!ReferralEligibility.CanShare(owner with { CodeActive = false }, service.Read().Applications));
        Check(!ReferralEligibility.CanShare(owner with { OnboardingComplete = false }, service.Read().Applications));
        var eligiblePortal = (PortalModel)((ViewResult)controller.Index()).Model!;
        Check(eligiblePortal.CanShare(eligiblePortal.Current!) && eligiblePortal.SharingUnavailableReason == null);
        Check(service.ReferralOwner(owner.Code).Id == owner.Id);
        service.SendInvitation(actor, owner.Id, "WhatsApp Friend", "WhatsApp", "Please explore this referral link.", "4165550123");
        service.SendInvitation(ops, owner.Id, "LinkedIn Friend", "LinkedIn", "Please explore this referral link.", "linkedin-friend");
        service.Refer(actor, "Direct Friend", "eligibility-direct@example.com", "4165550123");
        service.Attach(new(recipient.Id), recipient.Id, owner.Code);
        service.SubmitEnquiryAndRegister(owner.Code, null, "Enquiry Friend", "eligibility-enquiry@example.com", "4165550123", "General enquiry", true);
        Check(service.Read().Applications.Length == 0 && service.Read().Rewards.Length == 0);
        service.ToggleCode(ops, owner.Id);
        AssertBlocked();
        owner = service.Register("Pending Owner", "eligibility-pending@example.com", null, onboardingRequired: true);
        actor = new Actor(owner.Id);
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()) }, "test"));
        AssertBlocked();
        Console.WriteLine("PASS Referral eligibility: sharing without funded files, active-code and onboarding checks, admin/shadow protection, QR/link rejection and atomicity");
    }
}
