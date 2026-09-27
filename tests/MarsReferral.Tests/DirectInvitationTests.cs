using System.Text.Json;
using MarsReferral.Core;
using MarsReferral.Web.Controllers;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal static class DirectInvitationTests
{
    public static void Run()
    {
        static void Check(bool value) { if (!value) throw new Exception("Direct invitation assertion failed"); }
        static void Reject(Action action)
        {
            try { action(); }
            catch (RuleException) { return; }
            throw new Exception("Expected validation error");
        }
        static string State(ReferralService service) => JsonSerializer.Serialize(service.Read());
        using var service = new ReferralService();
        var owner = service.Register("Invitation Owner", "invitation-owner@example.com", null);
        var invitation = service.Refer(new(owner.Id), "Proposed Name", "proposed@example.com", "4165550192");
        Check(service.Read().Customers.Length == 1 && service.Read().Enquiries.Length == 0 && service.Read().Rewards.Length == 0);
        Check(service.Read().Messages.All(x => x.CustomerId == owner.Id));
        var before = State(service);
        var landing = new ReferralController(service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        for (var visit = 0; visit < 2; visit++)
        {
            var page = (ViewResult)landing.Index(owner.Code, invitation.Id);
            Check(page.Model is ReferralLanding model && model.Input.InvitationId == invitation.Id && model.Input.Code == owner.Code && string.IsNullOrEmpty(model.Input.Name) && string.IsNullOrEmpty(model.Input.Email));
        }
        Check(State(service) == before);
        Reject(() => service.SubmitEnquiryAndRegister(owner.Code, invitation.Id, "Recipient Name", "recipient@example.com", "4165550193", "General enquiry", false));
        Reject(() => service.SubmitEnquiryAndRegister(owner.Code, "missing", "Recipient Name", "recipient@example.com", "4165550193", "General enquiry", true));
        Reject(() => service.Refer(new(owner.Id), " ", "valid@example.com", "4165550193"));
        Reject(() => service.Refer(new(owner.Id), "Valid Name", "invalid", "4165550193"));
        Check(State(service) == before);
        var result = service.SubmitEnquiryAndRegister(owner.Code, invitation.Id, "Recipient Name", "recipient@example.com", "4165550193", "General enquiry", true);
        Check(result.AccountCreated && result.Customer.Name == "Recipient Name" && result.Customer.Email == "recipient@example.com" && result.Customer.Phone == "4165550193");
        Check(result.Customer.ReferrerId == owner.Id && result.Enquiry.InvitationId == invitation.Id && result.Enquiry.ReferrerId == owner.Id);
        Check(service.Read().Customers.Length == 2 && service.Read().Rewards.Length == 0);
        before = State(service);
        Reject(() => service.SubmitEnquiryAndRegister(owner.Code, invitation.Id, result.Customer.Name, result.Customer.Email, result.Customer.Phone, "General enquiry", true));
        Check(State(service) == before);
        service.MarkContacted(new(IsOperations: true), result.Enquiry.Id);
        var reused = service.SubmitEnquiryAndRegister(owner.Code, invitation.Id, result.Customer.Name, result.Customer.Email, result.Customer.Phone, "General enquiry", true, result.Customer.Id);
        Check(!reused.AccountCreated && reused.Customer.Id == result.Customer.Id && service.Read().Customers.Length == 2);
        var other = service.Register("Other Sender", "other-sender@example.com", null);
        before = State(service);
        Reject(() => service.SubmitEnquiryAndRegister(other.Code, invitation.Id, "Mismatch Person", "mismatch@example.com", "4165550194", "General enquiry", true));
        Check(State(service) == before);
        Console.WriteLine("PASS Direct invitations: no account on referral or link visits, recipient-owned details and consent, tracked registration, existing-account protection, invalid links and atomic validation");
    }
}
