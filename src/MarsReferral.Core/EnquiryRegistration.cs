namespace MarsReferral.Core;
public record EnquiryRegistration(Enquiry Enquiry, Customer Customer, bool AccountCreated, bool ExistingReferralKept);
public partial class ReferralService
{
    public EnquiryRegistration SubmitEnquiryAndRegister(string? code, string? invitationId, string? name, string? email, string? phone, string? interest, bool consent, int? authenticatedCustomerId = null) => Mutate(() =>
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? "";
        var customer = customers.SingleOrDefault(x => x.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
        // Existing-account access requires the server-authenticated customer identity, never an email alone.
        Require(customer == null || customer.Id == authenticatedCustomerId, "An account already uses this email. Sign in to that customer account before submitting the enquiry.");
        var enquiry = SubmitEnquiry(code, invitationId, name, email, phone, interest, consent);
        var created = customer == null;
        var existingReferralKept = customer?.ReferrerId is int attached && attached != enquiry.ReferrerId;
        if (customer == null)
        {
            customer = Register(enquiry.Name, enquiry.Email, code);
            var withPhone = customer with { Phone = enquiry.Phone };
            customers[customers.IndexOf(customer)] = withPhone;
            customer = withPhone;
        }
        else
        {
            if (customer.ReferrerId == null) Attach(new(customer.Id), customer.Id, code);
            customer = Find(customer.Id);
            if (!customer.OnboardingComplete) CompleteOnboarding(new(customer.Id), customer.Id, true);
            customer = Find(customer.Id);
        }
        Log(new(customer.Id), "Enquiry account linked", $"Enquiry {enquiry.Id} linked to {customer.Name}'s demo account. " + (created ? "Account created automatically." : "Authenticated existing account reused; saved referral retained."), customer.Id);
        Notify(customer.Id, "Your enquiry is ready to track", "Your enquiry has been assigned to Sarah Mitchell. Open My enquiries in the customer portal to follow its progress. No reward is earned until qualifying funding and approval.");
        return new EnquiryRegistration(enquiry, customer, created, existingReferralKept);
    });
}
