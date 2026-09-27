using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MarsReferral.Core;

public enum ApplicationStage { Submitted, Approved, Funded, Cancelled }
public enum RewardStage { Pending, Approved, Rejected, Failed, Paid }
public record Customer(int Id, string Name, string Email, string Code, bool CodeActive, int? ReferrerId, DateTimeOffset? AttachedAt, DateTimeOffset JoinedAt, bool OnboardingComplete = true) { public string Phone { get; init; } = ""; }
public record LoanApplication(int Id, int CustomerId, string Purpose, decimal Amount, ApplicationStage Stage, DateTimeOffset CreatedAt, DateTimeOffset? FundedAt = null, decimal? ApprovedAmount = null);
public record Reward(int Id, int BeneficiaryId, int ReferredId, int ApplicationId, string Kind, decimal Amount, RewardStage Stage, string Note = "", string Reference = "");
public record Message(int Id, int CustomerId, string Title, string Body, DateTimeOffset CreatedAt);
public record AuditEntry(DateTimeOffset At, string Actor, string Action, string Detail, int? CustomerId = null);
public record Snapshot(Customer[] Customers, LoanApplication[] Applications, Reward[] Rewards, Message[] Messages, AuditEntry[] Audit)
{
    public Invitation[] Invitations { get; init; } = [];
    public Enquiry[] Enquiries { get; init; } = [];
    public int PresentationVersion { get; init; }
}
public record Actor(int CustomerId = 0, bool IsOperations = false, bool IsShadow = false) { public string Label => IsOperations ? "Admin" : IsShadow ? $"Admin shadow of customer #{CustomerId}" : $"Customer #{CustomerId}"; }
public class RuleException(string message) : Exception(message);

/// <summary>Process-local, thread-safe class lists. All reads return immutable record snapshots.</summary>
public partial class ReferralService
{
    private readonly object gate = new();
    private readonly List<Customer> customers = [];
    private readonly List<LoanApplication> applications = [];
    private readonly List<Reward> rewards = [];
    private readonly List<Message> messages = [];
    private readonly List<AuditEntry> audit = [];
    private int nextCustomer = 1, nextApplication = 1001, nextReward = 2001, nextMessage = 1;
    public const decimal WelcomeBonus = 25, ReferrerBonus = 50;
    public ReferralService() { }
    public Snapshot Read() { lock (gate) return new(customers.ToArray(), applications.ToArray(), rewards.ToArray(), messages.ToArray(), audit.OrderByDescending(x => x.At).ToArray()) { Invitations = invitations.ToArray(), Enquiries = enquiries.ToArray(), PresentationVersion = presentationVersion }; }
    private Customer Find(int id) => customers.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Customer was not found.");
    private static void Require(bool ok, string message) { if (!ok) throw new RuleException(message); }
    private void Access(Actor actor, int id) { Require(actor.IsOperations || actor.CustomerId == id, "You cannot change another customer's profile."); Find(id); }
    private static void Operations(Actor actor) => Require(actor.IsOperations, "This action requires the operations role.");
    private void Log(Actor actor, string action, string detail, int customerId) => audit.Add(new(DateTimeOffset.UtcNow, actor.Label, action, detail, customerId));
    private void Notify(int id, string title, string body) => messages.Add(new(nextMessage++, id, title, body, DateTimeOffset.UtcNow));
    private Customer ValidateCode(string? raw, int? ownId)
    {
        var code = raw?.Trim().ToUpperInvariant() ?? "";
        Require(Regex.IsMatch(code, "^[A-Z0-9]{6,16}$"), "Enter a referral code containing 6â€“16 letters or numbers.");
        var owner = customers.SingleOrDefault(x => x.Code == code) ?? throw new RuleException("This referral code does not exist.");
        Require(owner.CodeActive, "This referral code is paused. Ask your friend for an active code.");
        Require(owner.Id != ownId, "You cannot use your own referral code.");
        return owner;
    }
    public Customer Register(string? name, string? email, string? referralCode, bool onboardingRequired = false)
    {
        return Mutate(() =>
        {
            name = name?.Trim() ?? ""; email = email?.Trim().ToLowerInvariant() ?? "";
            Require(name.Length is >= 2 and <= 80 && !name.Any(char.IsControl), "Name must contain 2â€“80 characters.");
            Require(email.Length <= 160 && new EmailAddressAttribute().IsValid(email) && !email.Any(char.IsWhiteSpace), "Enter a valid email address.");
            Require(!customers.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)), "This email already has an account. Choose it on the demo sign-in page.");
            var owner = string.IsNullOrWhiteSpace(referralCode) ? null : ValidateCode(referralCode, null);
            var id = nextCustomer++; var now = DateTimeOffset.UtcNow;
            var user = new Customer(id, name, email, $"MARS{id:000000}", true, owner?.Id, owner == null ? null : now, now, !onboardingRequired);
            customers.Add(user); Log(new(id), "Account created", $"{name} joined" + (owner == null ? "." : $" using {owner.Code}."), id);
            if (owner != null) Notify(id, "Referral attached", $"You joined through {owner.Name}. Complete a new eligible funding event to enter reward review. Demo welcome benefit: $25 CAD.");
            return user;
        });
    }
    public void Attach(Actor actor, int id, string? code)
    {
        Mutate(() =>
        {
            Access(actor, id); var user = Find(id);
            Require(user.ReferrerId == null, "You have already used a referral code. Your saved code cannot be replaced.");
            var owner = ValidateCode(code, id);
            // Prevent a referral cycle, including A -> B -> A.
            for (Customer? cursor = owner; cursor != null; cursor = cursor.ReferrerId is int parent ? Find(parent) : null)
                Require(cursor.Id != id, "This code would create a circular referral relationship.");
            customers[customers.IndexOf(user)] = user with { ReferrerId = owner.Id, AttachedAt = DateTimeOffset.UtcNow };
            Log(actor, "Referral attached", $"{user.Name} attached {owner.Code} to their profile.", id);
            Notify(id, "Referral attached", "Your profile now has a referral. New eligible funding can enter reward review. The code cannot be changed.");
        });
    }
    public void ToggleCode(Actor actor, int id)
    {
        Mutate(() =>
        { Operations(actor); var user = Find(id); customers[customers.IndexOf(user)] = user with { CodeActive = !user.CodeActive }; Log(actor, "Code status changed", $"{user.Code}: {(!user.CodeActive ? "active" : "paused")}. Existing referrals remain attached.", id); });
    }
    public void CompleteOnboarding(Actor actor, int id, bool understood)
    {
        Mutate(() =>
        {
            Access(actor, id); var user = Find(id);
            Require(understood, "Confirm that you understand the demo referral rules to finish onboarding.");
            Require(!user.OnboardingComplete, "Onboarding is already complete.");
            customers[customers.IndexOf(user)] = user with { OnboardingComplete = true };
            Log(actor, "Onboarding completed", $"{user.Name} completed the referral introduction.", id);
        });
    }
    public void RecordShadow(Actor actor, int id, bool starting)
    {
        Mutate(() =>
        {
            Operations(actor); var user = Find(id);
            Log(actor, starting ? "Customer shadow started" : "Customer shadow ended", $"{user.Name}, customer #{id}. Actions in shadow mode are attributed to admin.", id);
        });
    }
    public void ApproveAmount(Actor actor, int id, decimal amount)
    {
        Mutate(() =>
        {
            Operations(actor);
            var app = applications.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Application was not found.");
            Require(amount >= 1000 && amount <= app.Amount && decimal.Round(amount, 2) == amount, "Approved amount must be at least 1,000, cannot exceed the requested amount, and must have at most two decimals.");
            Transition(actor, id, ApplicationStage.Approved);
            var approved = applications.Single(x => x.Id == id);
            applications[applications.IndexOf(approved)] = approved with { ApprovedAmount = amount };
            Log(actor, "Amount approved", $"Application #{id}: requested {app.Amount:N2}, approved {amount:N2} demo units.", app.CustomerId);
            Notify(app.CustomerId, "Application amount approved", $"Admin approved {amount:N2} demo units for application #{id}. Funding has not yet been confirmed.");
        });
    }
    public LoanApplication CreateApplication(Actor actor, int id, string? purpose, decimal amount)
    {
        return Mutate(() =>
        {
            Access(actor, id); purpose = purpose?.Trim() ?? "";
            Require(Find(id).OnboardingComplete, "Complete onboarding before creating an application.");
            Require(new[] { "Home purchase", "Refinance", "Home improvement", "Reverse mortgage", "Mortgage" }.Contains(purpose), "Choose a valid application purpose.");
            Require(amount is >= 1000 and <= 10000000 && decimal.Round(amount, 2) == amount, "Amount must be 1,000â€“10,000,000 with no more than two decimal places.");
            var app = new LoanApplication(nextApplication++, id, purpose, amount, ApplicationStage.Submitted, DateTimeOffset.UtcNow);
            applications.Add(app); Log(actor, "Application submitted", $"Application #{app.Id} for {Find(id).Name}.", id); return app;
        });
    }
    public void Transition(Actor actor, int id, ApplicationStage target)
    {
        Mutate(() =>
        {
            Operations(actor); var app = applications.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Application was not found.");
            Require((app.Stage == ApplicationStage.Submitted && target is ApplicationStage.Approved or ApplicationStage.Cancelled) || (app.Stage == ApplicationStage.Approved && target is ApplicationStage.Funded or ApplicationStage.Cancelled), "This application transition is not allowed or was already processed.");
            var now = DateTimeOffset.UtcNow;
            applications[applications.IndexOf(app)] = app with { Stage = target, FundedAt = target == ApplicationStage.Funded ? now : null, ApprovedAmount = target == ApplicationStage.Approved ? app.Amount : app.ApprovedAmount };
            Log(actor, "Application updated", $"Application #{id}: {target}.", app.CustomerId);
            if (target != ApplicationStage.Funded) return;
            var user = Find(app.CustomerId);
            if (!messages.Any(x => x.CustomerId == user.Id && x.Title == "Your funding is complete"))
                Notify(user.Id, "Your funding is complete", $"Your account credit is confirmed in this simulation. Invite a friend using {user.Code}. Visit the referral guide to explore the demo benefit and conditions. No message was sent externally.");
            // Qualification is once per referred profile, after attachment; no retroactive funding rewards.
            if (user.ReferrerId is not int ownerId || user.AttachedAt > now || rewards.Any(x => x.ReferredId == user.Id)) return;
            rewards.Add(new(nextReward++, user.Id, user.Id, app.Id, "Welcome bonus", WelcomeBonus, RewardStage.Pending));
            rewards.Add(new(nextReward++, ownerId, user.Id, app.Id, "Referral bonus", ReferrerBonus, RewardStage.Pending));
            Notify(user.Id, "Reward under review", "Your $25 welcome reward is awaiting operations approval.");
            Notify(ownerId, "A friend reached funding", $"{user.Name} reached qualifying funding. Your $50 referral reward is under review.");
        });
    }
    public void Review(Actor actor, int id, bool approve, string? reason)
    {
        Mutate(() =>
        {
            Operations(actor); var reward = rewards.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Reward was not found.");
            Require(reward.Stage == RewardStage.Pending, "Only a pending reward can be reviewed.");
            reason = reason?.Trim() ?? "";
            Require(reason.Length <= 300 && (approve || reason.Length >= 5), "Provide a rejection reason of 5â€“300 characters.");
            var stage = approve ? RewardStage.Approved : RewardStage.Rejected;
            rewards[rewards.IndexOf(reward)] = reward with { Stage = stage, Note = reason };
            Log(actor, "Reward reviewed", $"Reward #{id}: {stage}.", reward.BeneficiaryId); Notify(reward.BeneficiaryId, $"Reward {stage.ToString().ToLowerInvariant()}", $"Your {reward.Kind.ToLowerInvariant()} of ${reward.Amount:N2} CAD was {stage.ToString().ToLowerInvariant()}. {reason}");
        });
    }
    public void Pay(Actor actor, int id, string? reference, bool successful)
    {
        Mutate(() =>
        {
            Operations(actor); var reward = rewards.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("Reward was not found.");
            Require(reward.Stage is RewardStage.Approved or RewardStage.Failed, "Approve the reward before payout. Paid or rejected rewards cannot be paid.");
            reference = reference?.Trim().ToUpperInvariant() ?? "";
            Require(Regex.IsMatch(reference, "^[A-Z0-9-]{4,40}$"), "Enter a payment reference with 4â€“40 letters, numbers or hyphens.");
            Require(!rewards.Any(x => x.Id != id && x.Stage == RewardStage.Paid && x.Reference == reference), "This payment reference has already been used.");
            rewards[rewards.IndexOf(reward)] = reward with { Stage = successful ? RewardStage.Paid : RewardStage.Failed, Reference = reference, Note = successful ? "Demo payment confirmed" : "Demo payment failed. Safe to retry with a confirmed result." };
            Log(actor, "Payout simulated", $"Reward #{id}: {(successful ? "paid" : "failed")}, reference {reference}.", reward.BeneficiaryId);
            Notify(reward.BeneficiaryId, successful ? "Reward paid" : "Payment needs attention", successful ? $"Your ${reward.Amount:N2} CAD reward were paid in the demo. Reference: {reference}." : "The simulated payment failed. Operations can retry; no paid balance was added.");
        });
    }
}
