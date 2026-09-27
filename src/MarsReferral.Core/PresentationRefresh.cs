namespace MarsReferral.Core;
public partial class ReferralService
{
    public void RefreshPresentationDemo()
    {
        lock(gate) { if(presentationVersion >= 1) return; }
        Mutate(() => {
            if(presentationVersion >= 1) return;
            string[] names = ["Liam Thompson", "Olivia Martin", "Ethan Wilson", "Emma Anderson", "Noah Bennett", "Charlotte Clark", "James Walker", "Amelia Scott", "Lucas Parker", "Isabella Moore", "Henry Lewis", "Sophie Campbell", "Oliver Brooks", "Grace Miller", "William Taylor", "Ava Johnson"];
            var replacements=new Dictionary<string,string>();
            foreach(var old in customers.ToArray()) {
                var name=names[(old.Id-1)%names.Length] + (old.Id>names.Length ? $" {old.Id}" : "");
                replacements[old.Name]=name;
                var email=old.Email.EndsWith("@example.com",StringComparison.OrdinalIgnoreCase)?$"{name.ToLowerInvariant().Replace(' ','.')}@example.com":old.Email;
                customers[customers.IndexOf(old)] = old with {Name=name,Email=email};
            }
            string Update(string text) {
                foreach(var pair in replacements.OrderByDescending(x=>x.Key.Length)) text=text.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
                return text.Replace("1,000-credit","$50").Replace("500-credit","$25").Replace("1,000 credits","$50 CAD").Replace("1000 credits","$50 CAD").Replace("500 credits","$25 CAD");
            }
            for(var i=0;i<rewards.Count;i++) rewards[i]=rewards[i] with {Amount=rewards[i].Kind=="Welcome bonus"?WelcomeBonus:ReferrerBonus,Note=Update(rewards[i].Note)};
            for(var i=0;i<messages.Count;i++) messages[i]=messages[i] with {Title=Update(messages[i].Title),Body=Update(messages[i].Body)};
            for(var i=0;i<audit.Count;i++) audit[i]=audit[i] with {Detail=Update(audit[i].Detail)};
            presentationVersion=1;
        });
    }
}
