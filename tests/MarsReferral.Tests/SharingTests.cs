using MarsReferral.Core;
using System.Text.Json;
internal static class SharingTests {
 public static void Run() {
  static void Check(bool ok){if(!ok)throw new Exception("Sharing assertion failed");}
  static void Reject(Action f){try{f();throw new Exception("Expected rejection");}catch(RuleException){}}
  var ops=new Actor(IsOperations:true);using var s=new ReferralService();
  var a=s.Register("Liam Thompson","liam@example.com",null);var b=s.Register("Olivia Martin","olivia@example.com",null);
  ReferralTestSetup.Fund(s,a.Id);ReferralTestSetup.Fund(s,b.Id);
  var invite=s.SendInvitation(new(a.Id),a.Id,"Sophie Campbell","WhatsApp","Please explore the referral link below.","+1 (416) 555-0123");
  Check(invite.Contact=="+14165550123");
  Check(ReferralService.NormalizeInvitationContact("LinkedIn","@sophie-campbell")=="https://www.linkedin.com/in/sophie-campbell");
  Check(ReferralService.NormalizeInvitationContact("LinkedIn","https://www.linkedin.com/in/sophie-campbell/?trk=demo")=="https://www.linkedin.com/in/sophie-campbell");
  foreach(var invalid in new[]{"", "123", "0000000000", "14165550123abc", "https://linkedin.com/in/sophie"}) Reject(()=>ReferralService.NormalizeInvitationContact("WhatsApp",invalid));
  foreach(var invalid in new[]{"", "14165550123", "ab", "https://evil.example/in/person", "https://linkedin.com.evil.example/in/person", "http://linkedin.com/in/person", "https://linkedin.com/company/person", "https://person@linkedin.com/in/person"}) Reject(()=>ReferralService.NormalizeInvitationContact("LinkedIn",invalid));
  Reject(()=>s.SendInvitation(new(a.Id),a.Id,"Friend","WhatsApp","Please view this invitation"));
  Check(invite.Status=="Sent" && s.Read().Invitations.Length==1 && s.Read().Rewards.Length==0);
  Reject(()=>s.SendInvitation(new(b.Id),a.Id,"Friend","WhatsApp","Hello this is a message"));
  Reject(()=>s.SendInvitation(new(a.Id),a.Id,"Friend","SMS","Hello this is a message"));
  Reject(()=>s.DeliverInvitation(new(b.Id),invite.Id));s.DeliverInvitation(new(a.Id),invite.Id);Reject(()=>s.DeliverInvitation(new(a.Id),invite.Id));
  Check(s.Read().Invitations.Single().Status=="Delivered");
  var e=s.SubmitEnquiry(a.Code,invite.Id,"Sophie Campbell","sophie@example.com","+1 416 555 0123","Reverse mortgage",true);
  Check(e.Salesperson=="Sarah Mitchell" && e.ReferrerId==a.Id && e.InvitationId==invite.Id && s.Read().Rewards.Length==0);
  Reject(()=>s.SubmitEnquiry(a.Code,invite.Id,"Sophie Campbell","sophie@example.com","4165550123","Reverse mortgage",true));
  Reject(()=>s.SubmitEnquiry(b.Code,invite.Id,"Friend Two","two@example.com","4165550123","Reverse mortgage",true));
  Reject(()=>s.SubmitEnquiry(a.Code,null,"Friend Two","two@example.com","4165550123","Reverse mortgage",false));
  Reject(()=>s.SubmitEnquiry(a.Code,null,"Friend Two",a.Email,"4165550123","Reverse mortgage",true));
  Reject(()=>s.SubmitEnquiry(a.Code,null,"Friend Two","two@example.com","123","Reverse mortgage",true));
  Reject(()=>s.MarkContacted(new(a.Id),e.Id));s.MarkContacted(ops,e.Id);Check(s.Read().Enquiries.Single().Status=="Contacted");Reject(()=>s.MarkContacted(ops,e.Id));
  s.ToggleCode(ops,a.Id);Reject(()=>s.SendInvitation(new(a.Id),a.Id,"Friend","LinkedIn","Hello this is a message"));Reject(()=>s.SubmitEnquiry(a.Code,null,"Friend Two","two@example.com","4165550123","Reverse mortgage",true));
  Check(s.Read().Invitations.Length==1 && s.Read().Enquiries.Length==1);
  var path=Path.Combine(Path.GetTempPath(),"MarsSharing-"+Guid.NewGuid().ToString("N")+".json");
  try{
   File.WriteAllText(path,JsonSerializer.Serialize(s.Read()));
   using(var persisted=new ReferralService(path)){Check(persisted.Read().Enquiries.Single()==s.Read().Enquiries.Single());Check(persisted.Read().Invitations.Single()==s.Read().Invitations.Single());}
   // Older JSON has no sharing fields: it must still open without discarding records.
   File.WriteAllText(path,JsonSerializer.Serialize(new{Customers=s.Read().Customers,Applications=s.Read().Applications,Rewards=s.Read().Rewards,Messages=s.Read().Messages,Audit=s.Read().Audit}));
   using(var legacy=new ReferralService(path)){Check(legacy.Read().Customers.Length==2 && legacy.Read().Invitations.Length==0);legacy.RefreshPresentationDemo();var after=JsonSerializer.Serialize(legacy.Read());legacy.RefreshPresentationDemo();Check(JsonSerializer.Serialize(legacy.Read())==after);}
  }finally{File.Delete(path);}
  Console.WriteLine("PASS Sharing and enquiries: ownership, validation, delivery, consent, paused code, follow-up, persistence and legacy migration");
 }
}
