using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
namespace MarsReferral.Core;
public record Invitation(string Id, int ReferrerId, string Recipient, string Channel, string Body, string Status, DateTimeOffset CreatedAt) { public string Contact { get; init; } = ""; }
public record Enquiry(string Id, int ReferrerId, string? InvitationId, string Name, string Email, string Phone, string Interest, string Salesperson, string Status, DateTimeOffset CreatedAt);
public partial class ReferralService
{
    private readonly List<Invitation> invitations = [];
    private readonly List<Enquiry> enquiries = [];
    private int presentationVersion;
    // Populate an empty POC sales queue once; preserve existing enquiries and follow-up changes.
    public void SeedDemoEnquiries()
    {
        lock (gate)
        {
            if (enquiries.Count != 0) return;
            var owners = customers.Where(x => x.CodeActive && x.OnboardingComplete).OrderBy(x => x.Id).Take(3).ToArray();
            if (owners.Length == 0) return;
            Mutate(() => {
                var now = DateTimeOffset.UtcNow;
                var samples = new[] {
                    ("Grace Miller", "grace.miller.enquiry@example.com", "+1 416 555 0101", "Reverse mortgage", "New"),
                    ("Oliver Brooks", "oliver.brooks.enquiry@example.com", "+1 604 555 0102", "Income Solution", "New"),
                    ("Ava Johnson", "ava.johnson.enquiry@example.com", "+1 613 555 0103", "General enquiry", "New"),
                    ("William Taylor", "william.taylor.enquiry@example.com", "+1 403 555 0104", "Reverse mortgage", "Contacted"),
                    ("Sophie Campbell", "sophie.campbell.enquiry@example.com", "+1 902 555 0105", "Income Solution", "Contacted")
                };
                for (var i = 0; i < samples.Length; i++)
                {
                    var (name, email, phone, interest, status) = samples[i];
                    enquiries.Add(new Enquiry(Guid.NewGuid().ToString("N"), owners[i % owners.Length].Id,
                        null, name, email, phone, interest, "Sarah Mitchell", status, now.AddDays(-i - 1)));
                    Log(new(IsOperations: true), "Sample enquiry loaded", $"Fictional POC enquiry for {name}; status {status}. No external communication occurred.", owners[i % owners.Length].Id);
                }

            });
        }
    }
    public Customer ReferralOwner(string? code) { lock(gate) return ValidateCode(code, null); }
    public Invitation SendInvitation(Actor actor, int referrerId, string? recipient, string? channel, string? body, string? contact = null) => Mutate(() => {
        Access(actor, referrerId); var owner = Find(referrerId);
        RequireReferralEligibility(owner);
        recipient = recipient?.Trim() ?? ""; body = body?.Trim() ?? "";
        Require(recipient.Length is >= 2 and <= 80 && !recipient.Any(char.IsControl), "Enter a recipient name of 2 to 80 characters.");
        Require(channel is "WhatsApp" or "LinkedIn", "Choose WhatsApp or LinkedIn.");
        Require(body.Length is >= 10 and <= 1200 && !body.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'), "Enter a message of 10 to 1,200 characters.");
        var destination = NormalizeInvitationContact(channel!, contact);
        var invite = new Invitation(Guid.NewGuid().ToString("N"), owner.Id, recipient, channel!, body, "Sent", DateTimeOffset.UtcNow) { Contact = destination };
        invitations.Add(invite); Log(actor, "Invitation sent (demo)", $"{channel} invitation to {recipient}. Referral ID {invite.Id}. No external message sent.", owner.Id);
        Notify(owner.Id, "Referral link sent (demo)", $"Your {channel} invitation to {recipient} was recorded. Open Sharing demo to simulate delivery and view the recipient experience.");
        return invite;
    });
    public static string NormalizeInvitationContact(string channel, string? value)
    {
        value = value?.Trim() ?? "";
        Require(value.Length is > 0 and <= 250 && !value.Any(char.IsControl), "Enter the recipient contact for the selected channel.");
        if (channel == "WhatsApp")
        {
            var digits = Regex.Replace(value, "[^0-9]", "");
            Require(value.Length <= 30 && Regex.IsMatch(value, @"^\+?[0-9 ()\-.]+$") && digits.Length is >= 10 and <= 15 && digits[0] != '0',
                "Enter a valid WhatsApp number with country code (10 to 15 digits), for example +1 416 555 0123.");
            return "+" + digits;
        }
        Require(channel == "LinkedIn", "Choose WhatsApp or LinkedIn.");
        var profileId = value.TrimStart('@');
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            Require(uri.Scheme == "https" && (uri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase))
                && uri.UserInfo.Length == 0 && uri.IsDefaultPort && uri.AbsolutePath.StartsWith("/in/", StringComparison.Ordinal),
                "Enter a LinkedIn profile ID or an https://www.linkedin.com/in/ profile link.");
            profileId = uri.AbsolutePath[4..].TrimEnd('/');
        }
        Require(Regex.IsMatch(profileId, @"^(?=[A-Za-z0-9_-]*[A-Za-z])[A-Za-z0-9][A-Za-z0-9_-]{2,99}$"),
            "Enter a valid LinkedIn profile ID (3 to 100 letters, numbers, hyphens or underscores) or a LinkedIn /in/ profile link.");
        return "https://www.linkedin.com/in/" + profileId;
    }
    public void DeliverInvitation(Actor actor, string id) => Mutate(() => {
        var i = invitations.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Invitation was not found.");
        Access(actor,i.ReferrerId);
        Require(i.Channel is "WhatsApp" or "LinkedIn" && i.Status == "Sent", "Only sent WhatsApp or LinkedIn invitations can be delivered in the demo.");
        invitations[invitations.IndexOf(i)] = i with { Status = "Delivered" };
        Log(actor,"Invitation delivered (demo)",$"Referral ID {id} delivered to {i.Recipient} in the simulated recipient view.",i.ReferrerId);
    });
    public Enquiry SubmitEnquiry(string? code, string? invitationId, string? name, string? email, string? phone, string? interest, bool consent) => Mutate(() => {
        var owner = ValidateCode(code,null);
        if (!string.IsNullOrEmpty(invitationId)) Require(invitations.Any(x => x.Id == invitationId && x.ReferrerId == owner.Id), "The referral invitation is invalid.");
        name=name?.Trim() ?? ""; email=email?.Trim().ToLowerInvariant() ?? ""; phone=phone?.Trim() ?? "";
        Require(name.Length is >= 2 and <= 80 && !name.Any(char.IsControl), "Enter your full name (2 to 80 characters).");
        Require(email.Length <=160 && new EmailAddressAttribute().IsValid(email) && !string.IsNullOrWhiteSpace(email) && !email.Any(char.IsWhiteSpace), "Enter a valid email address.");
        var digits=Regex.Replace(phone,"[^0-9]","");
        Require(phone.Length <=30 && Regex.IsMatch(phone,@"^\+?[0-9 ()\-.]+$") && digits.Length is >=10 and <=15 && digits.Any(c=>c!='0'),"Enter a phone number containing 10 to 15 digits.");
        Require(consent,"Confirm that you would like a salesperson to contact you.");
        Require(interest is "Reverse mortgage" or "Income Solution" or "General enquiry","Choose a valid enquiry topic.");
        Require(!email.Equals(owner.Email,StringComparison.OrdinalIgnoreCase),"You cannot refer yourself.");
        Require(!enquiries.Any(x=>x.Email==email && x.Status=="New"),"An enquiry for this email is already awaiting follow-up.");
        var e=new Enquiry(Guid.NewGuid().ToString("N"),owner.Id,string.IsNullOrEmpty(invitationId)?null:invitationId,name,email,phone,interest!,"Sarah Mitchell","New",DateTimeOffset.UtcNow);
        enquiries.Add(e); Log(new(IsOperations:true),"Enquiry routed (demo)",$"Enquiry {e.Id} assigned to Sarah Mitchell, demo salesperson.",owner.Id);
        Notify(owner.Id,"Your referral has made an enquiry","A referred visitor has requested a salesperson follow-up. Rewards still require qualifying funding and approval.");
        return e;
    });
    public void MarkContacted(Actor actor,string id) => Mutate(() => {
        Operations(actor);var e=enquiries.SingleOrDefault(x=>x.Id==id) ?? throw new RuleException("Enquiry was not found.");
        Require(e.Status=="New","This enquiry is already marked contacted.");enquiries[enquiries.IndexOf(e)]=e with {Status="Contacted"};
        Log(actor,"Sales follow-up simulated",$"Sarah Mitchell marked enquiry {id} contacted. No external call or message was made.",e.ReferrerId);
    });
}
