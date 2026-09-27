using System.Text.RegularExpressions;
namespace MarsReferral.Core;
public partial class ReferralService
{
    public Customer Refer(Actor actor, string? name, string? email, string? phone) => Mutate(() =>
    {
        Require(!actor.IsOperations && actor.CustomerId > 0, "Sign in to the customer portal to refer someone.");
        var owner = Find(actor.CustomerId);
        Require(owner.OnboardingComplete, "Complete your onboarding before referring someone.");
        Require(owner.CodeActive, "Your referral code is paused. Contact the admin team.");
        phone = phone?.Trim() ?? "";
        var digits = Regex.Replace(phone, "[^0-9]", "");
        Require(phone.Length <= 30 && Regex.IsMatch(phone, @"^\+?[0-9 ()\-.]+$") && digits.Length is >= 10 and <= 15 && digits.Any(c => c != '0'), "Enter a valid phone number with 10 to 15 digits; include the country code where needed.");
        var customer = Register(name, email, owner.Code);
        var saved = customer with { Phone = phone };
        customers[customers.IndexOf(customer)] = saved;
        Log(actor, "Customer referred directly", $"Created a demo account for {saved.Name} using {owner.Code}.", owner.Id);
        Notify(owner.Id, "Your circle has grown", $"{saved.Name} can now choose their name on customer demo sign-in. Rewards require eligible funding and approval.");
        Notify(saved.Id, "Your demo account is ready", $"{owner.Name} referred you. Your referral code is already attached to your profile.");
        return saved;
    });
}
