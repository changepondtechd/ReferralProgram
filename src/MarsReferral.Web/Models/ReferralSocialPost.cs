using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace MarsReferral.Web.Models;

public record ReferralSocialPost(string ReferralUrl)
{
    public string Text => "Discover HomeEquity Bank's CHIP Reverse Mortgage: eligible Canadian homeowners aged 55+ can access home equity as tax-free cash while continuing to live in their home. Eligibility and loan terms apply.\n\n" +
        "Through this Customer-to-Customer referral demo, you could receive $25 CAD and I could receive $50 CAD after qualifying funding and approval. These illustrative rewards are not an approved HomeEquity Bank offer.\n\n" +
        "Interested? Register and submit an enquiry using my link below. My referral code is included automatically.\n" + ReferralUrl;
    public string TwitterText => "Explore HomeEquity Bank's CHIP Reverse Mortgage for eligible homeowners 55+. Customer-to-Customer referral: register via my link. POC demo rewards only; not an approved offer. Eligibility and terms apply.";
    public string LinkedInUrl => QueryHelpers.AddQueryString("https://www.linkedin.com/sharing/share-offsite/", "url", ReferralUrl);
    public string TwitterUrl => QueryHelpers.AddQueryString("https://twitter.com/intent/tweet", new Dictionary<string, string?> { ["text"] = TwitterText, ["url"] = ReferralUrl });
    public string FacebookUrl => QueryHelpers.AddQueryString("https://www.facebook.com/sharer/sharer.php", "u", ReferralUrl);
    public bool IsLocalAddress => Uri.TryCreate(ReferralUrl, UriKind.Absolute, out var uri) && uri.IsLoopback;

    public static ReferralSocialPost? Create(PortalModel portal, IUrlHelper urls)
    {
        if (portal.IsOperations || portal.Current is not { } customer || !portal.CanShare(customer)) return null;
        var request = urls.ActionContext.HttpContext.Request;
        var link = urls.Action("Index", "Referral", new { code = customer.Code }, request.Scheme, request.Host.Value);
        return string.IsNullOrEmpty(link) ? null : new(link);
    }
}
