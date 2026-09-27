using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
namespace MarsReferral.Core;
public partial class ReferralService
{
    public Invitation Refer(Actor actor, string? name, string? email, string? phone) => Mutate(() =>
    {
        Require(!actor.IsOperations && actor.CustomerId > 0, "Sign in to the customer portal to refer someone.");
        var owner = Find(actor.CustomerId);
        RequireReferralEligibility(owner);
        name = name?.Trim() ?? "";
        email = email?.Trim().ToLowerInvariant() ?? "";
        Require(name.Length is >= 2 and <= 80 && !name.Any(char.IsControl), "Name must contain 2 to 80 characters.");
        Require(email.Length <= 160 && new EmailAddressAttribute().IsValid(email) && !email.Any(char.IsWhiteSpace), "Enter a valid email address.");
        phone = phone?.Trim() ?? "";
        var digits = Regex.Replace(phone, "[^0-9]", "");
        Require(phone.Length <= 30 && Regex.IsMatch(phone, @"^\+?[0-9 ()\-.]+$") && digits.Length is >= 10 and <= 15 && digits.Any(c => c != '0'), "Enter a valid phone number with 10 to 15 digits; include the country code where needed.");
        Require(!customers.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)), "This email already has an account. Ask your friend to sign in before submitting a referral enquiry.");
        Require(!invitations.Any(x => x.ReferrerId == owner.Id && x.Channel == "Direct" && x.Contact.StartsWith(email + " · ", StringComparison.OrdinalIgnoreCase)), "An invitation for this email has already been recorded. Share the existing link from Messages.");
        var invitation = new Invitation(Guid.NewGuid().ToString("N"), owner.Id, name, "Direct",
            $"Hi {name}, {owner.Name} has invited you to explore home equity options. Open the referral link and submit your own details and consent to create your demo account using referral code {owner.Code}. Opening the link does not create an account.",
            "Recorded", DateTimeOffset.UtcNow) { Contact = $"{email} · {phone}" };
        invitations.Add(invitation);
        Log(actor, "Direct invitation recorded", $"Invitation {invitation.Id} recorded for {name} using {owner.Code}. No account created or external message sent.", owner.Id);
        Notify(owner.Id, "Your invitation is ready", $"Share {name}'s invitation link from Messages. They must submit their own details and consent to create an account. No email or SMS was sent.");
        return invitation;
    });
}
