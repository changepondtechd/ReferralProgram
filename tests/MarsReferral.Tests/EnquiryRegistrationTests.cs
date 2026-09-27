using MarsReferral.Core;
using System.Text.Json;
internal static class EnquiryRegistrationTests
{
 public static void Run(){
  static void Check(bool ok){if(!ok)throw new Exception("Enquiry registration assertion failed");}
  static void Reject(Action action){try{action();throw new Exception("Expected rejection");}catch(RuleException){}}
  using var s=new ReferralService();var owner=s.Register("Liam Thompson","liam@example.com",null);
  ReferralTestSetup.Fund(s,owner.Id);
  var result=s.SubmitEnquiryAndRegister(owner.Code,null,"Ava Harper","ava@example.com","+1 416 555 0188","Reverse mortgage",true);
  Check(result.AccountCreated && result.Customer.OnboardingComplete && result.Customer.ReferrerId==owner.Id && result.Customer.Phone=="+1 416 555 0188" && s.Read().Rewards.Length==0);
  Check(result.Enquiry.Email==result.Customer.Email && s.Read().Enquiries.Length==1);
  var before=JsonSerializer.Serialize(s.Read());
  Reject(()=>s.SubmitEnquiryAndRegister(owner.Code,null,"Ava Harper","ava@example.com","4165550188","Reverse mortgage",true));
  Reject(()=>s.SubmitEnquiryAndRegister(owner.Code,null,"Ava Harper","ava@example.com","4165550188","Reverse mortgage",true,owner.Id));
  Check(JsonSerializer.Serialize(s.Read())==before);
  s.MarkContacted(new(IsOperations:true),result.Enquiry.Id);
  var reused=s.SubmitEnquiryAndRegister(owner.Code,null,"Ava Harper","AVA@EXAMPLE.COM","4165550188","Income Solution",true,result.Customer.Id);
  Check(!reused.AccountCreated && reused.Customer.Id==result.Customer.Id && s.Read().Customers.Length==2);
  var existing=s.Register("Olivia Martin","olivia@example.com",null);
  var attached=s.SubmitEnquiryAndRegister(owner.Code,null,existing.Name,existing.Email,"4165550190","General enquiry",true,existing.Id);
  Check(attached.Customer.ReferrerId==owner.Id);
  ReferralTestSetup.Fund(s,existing.Id);
  var other=s.Register("Ethan Wilson","ethan@example.com",existing.Code);
  var retained=s.SubmitEnquiryAndRegister(owner.Code,null,other.Name,other.Email,"4165550191","General enquiry",true,other.Id);
  Check(retained.ExistingReferralKept && retained.Customer.ReferrerId==existing.Id);
  var parent=s.Register("Parent Person","parent@example.com",null);ReferralTestSetup.Fund(s,parent.Id);var child=s.Register("Child Person","child@example.com",parent.Code);ReferralTestSetup.Fund(s,child.Id);
  before=JsonSerializer.Serialize(s.Read());
  Reject(()=>s.SubmitEnquiryAndRegister(child.Code,null,parent.Name,parent.Email,"4165550192","General enquiry",true,parent.Id));
  Reject(()=>s.SubmitEnquiryAndRegister(owner.Code,null,"No Consent","no-consent@example.com","4165550193","General enquiry",false));
  Check(JsonSerializer.Serialize(s.Read())==before);
  int winners=0;Parallel.For(0,6,_=>{try{s.SubmitEnquiryAndRegister(owner.Code,null,"Single Person","single@example.com","4165550194","General enquiry",true);Interlocked.Increment(ref winners);}catch(RuleException){}});
  Check(winners==1 && s.Read().Customers.Count(x=>x.Email=="single@example.com")==1 && s.Read().Enquiries.Count(x=>x.Email=="single@example.com")==1);
  var path=Path.Combine(Path.GetTempPath(),"EnquiryRegistration-"+Guid.NewGuid()+".json");
  try{File.WriteAllText(path,JsonSerializer.Serialize(s.Read()));using var saved=new ReferralService(path);Check(saved.Read().Customers.Single(x=>x.Id==result.Customer.Id)==result.Customer);}finally{File.Delete(path);}
  Console.WriteLine("PASS Enquiry auto-registration: new account, ready portal, no early reward, authenticated reuse, no email-only access, referral preservation, atomic rollback, concurrent duplicates and persistence");
 }
}
