namespace MarsReferral.Core;
public static class ReferralEligibility
{
    public static string? UnavailableReason(Customer customer, IEnumerable<LoanApplication> applications)
    {
        if (!customer.OnboardingComplete) return "Complete your onboarding before referring someone.";
        if (!customer.CodeActive) return "Your referral code is paused. Contact the admin team.";
        return null;
    }
    public static bool CanShare(Customer customer, IEnumerable<LoanApplication> applications) => UnavailableReason(customer, applications) == null;
}
public partial class ReferralService
{
    private void RequireReferralEligibility(Customer owner)
    {
        var reason = ReferralEligibility.UnavailableReason(owner, applications);
        Require(reason == null, reason ?? "Referral sharing is unavailable.");
    }
}
