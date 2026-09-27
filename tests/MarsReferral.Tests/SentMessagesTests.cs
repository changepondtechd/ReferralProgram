using System.Security.Claims;
using System.Text.Json;
using MarsReferral.Core;
using MarsReferral.Web.Controllers;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal static class SentMessagesTests
{
 public static void Run()
 {
  static void Check(bool value){if(!value)throw new Exception("Sent messages assertion failed");}
  static void Reject(Action action){try{action();throw new Exception("Expected rejection");}catch(RuleException){}}
  static string State(ReferralService service)=>JsonSerializer.Serialize(service.Read());
  using var service=new ReferralService();
  var owner=service.Register("Message Owner","messages-owner@example.com",null);
  var other=service.Register("Other Owner","messages-other@example.com",null);
  ReferralTestSetup.Fund(service,owner.Id);ReferralTestSetup.Fund(service,other.Id);
  var actor=new Actor(owner.Id);
  var body="Hi Sophie,\nPlease explore your referral link below.";
  var whatsapp=service.SendInvitation(actor,owner.Id,"Sophie Campbell","WhatsApp",body,"+1 416 555 0190");
  var linkedin=service.SendInvitation(actor,owner.Id,"Oliver Brooks","LinkedIn","Explore this introduction and referral link.","oliver-brooks");
  var foreign=service.SendInvitation(new(other.Id),other.Id,"Private Friend","WhatsApp","Another customer's private invitation.","+1 416 555 0191");
  var invitation=service.Refer(actor,"Direct Friend","direct-message@example.com","+1 416 555 0192");
  var direct=service.Read().Invitations.Single(x=>x.Channel=="Direct");
  Check(direct==invitation && direct.ReferrerId==owner.Id && direct.Recipient=="Direct Friend" && direct.Status=="Recorded");
  Check(direct.Contact.Contains("direct-message@example.com") && direct.Contact.Contains("+1 416 555 0192") && direct.Body.Contains(owner.Code));
  Check(service.Read().Customers.Length==2 && service.Read().Messages.All(x=>x.CustomerId==owner.Id || x.CustomerId==other.Id));
  var before=State(service);
  Reject(()=>service.Refer(actor,"Duplicate Friend","DIRECT-MESSAGE@EXAMPLE.COM","4165550192"));
  Reject(()=>service.Refer(actor,"Invalid Friend","invalid-message@example.com","bad phone"));
  Reject(()=>service.DeliverInvitation(actor,direct.Id));
  Check(State(service)==before);
  service.DeliverInvitation(actor,whatsapp.Id);

  var context=new DefaultHttpContext();
  context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,owner.Id.ToString()),new Claim(ClaimTypes.Role,"Customer")},"test"));
  context.Request.QueryString=new QueryString($"?customerId={other.Id}");
  var controller=new CustomerController(service){ControllerContext=new ControllerContext{HttpContext=context}};
  var view=controller.Messages() as ViewResult;
  Check(view?.ViewName=="~/Views/Portal/Messages.cshtml");
  var model=(PortalModel)view!.Model!;
  Check(model.Pagination.Total==3 && model.Data.Invitations.All(x=>x.ReferrerId==owner.Id));
  Check(model.Data.Invitations.Single(x=>x.Id==whatsapp.Id).Body==body && model.Data.Invitations.Single(x=>x.Id==whatsapp.Id).Status=="Delivered");
  Check(model.Data.Invitations.Any(x=>x.Id==linkedin.Id) && model.Data.Invitations.Any(x=>x.Id==direct.Id));
  Check(!model.Data.Invitations.Any(x=>x.Id==foreign.Id));
  var sharing=(SharingModel)((ViewResult)controller.Sharing(null)).Model!;
  Check(sharing.Portal.Data.Invitations.Length==2 && sharing.Portal.Data.Invitations.All(x=>x.Channel!="Direct"));
  Check(controller.Sharing(direct.Id) is NotFoundResult && controller.Sharing(foreign.Id) is NotFoundResult);

  var landing=new ReferralController(service){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};
  foreach(var message in model.Data.Invitations){
   var page=(ViewResult)landing.Index(owner.Code,message.Id);
   Check(page.Model is ReferralLanding referral && referral.Owner.Id==owner.Id && referral.Input.InvitationId==message.Id && referral.Input.Code==owner.Code);
  }
  for(var index=0;index<12;index++)service.SendInvitation(actor,owner.Id,$"Friend {index}","WhatsApp","Here is your personal referral link.","+1 416 555 0193");
  var expected=service.Read().Invitations.Where(x=>x.ReferrerId==owner.Id).OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).ToArray();
  var first=(PortalModel)((ViewResult)controller.Messages(1,10)).Model!;
  var second=(PortalModel)((ViewResult)controller.Messages(2,10)).Model!;
  Check(first.Pagination.Total==15 && first.Data.Invitations.Length==10 && second.Data.Invitations.Length==5);
  Check(first.Data.Invitations.Concat(second.Data.Invitations).SequenceEqual(expected));
  var clamped=(PortalModel)((ViewResult)controller.Messages(int.MaxValue,7)).Model!;
  Check(clamped.Pagination.PageSize==10 && clamped.Pagination.PageNumber==2);
  var empty=service.Register("Empty Owner","empty-messages@example.com",null);
  context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,empty.Id.ToString())},"test"));
  Check(((PortalModel)((ViewResult)controller.Messages()).Model!).Pagination.Total==0);

  var directory=Path.Combine(Path.GetTempPath(),"SentMessages-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  var path=Path.Combine(directory,"data.json");
  try{
   File.WriteAllText(path,State(service));
   using(var saved=new ReferralService(path)){
    Check(saved.Read().Invitations.SequenceEqual(service.Read().Invitations));
    var snapshot=State(saved);
    using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
     Reject(()=>saved.Refer(actor,"Unsaved Friend","unsaved-message@example.com","+1 416 555 0194"));
     Check(State(saved)==snapshot);
    }
    Check(File.ReadAllText(path)==snapshot);
    var count=saved.Read().Invitations.Length;var winners=0;
    Parallel.For(0,6,_=>{try{saved.Refer(actor,"Concurrent Friend","concurrent-message@example.com","+1 416 555 0195");Interlocked.Increment(ref winners);}catch(RuleException){}});
    Check(winners==1 && saved.Read().Invitations.Length==count+1);
   }
   using(var reopened=new ReferralService(path))Check(reopened.Read().Invitations.Count(x=>x.Channel=="Direct")==2);
   var invalid=service.Read() with { Invitations=service.Read().Invitations.Select(x=>x.Id==direct.Id?x with { Status="Sent" }:x).ToArray() };
   File.WriteAllText(path,JsonSerializer.Serialize(invalid));
   try{using var rejected=new ReferralService(path);throw new Exception("Invalid direct status was accepted");}catch(InvalidOperationException ex){Check(ex.InnerException is InvalidDataException);}
  }finally{Directory.Delete(directory,true);}
  Console.WriteLine("PASS Sent messages: ownership, message content, tracked links, direct receipts, social isolation, pagination, persistence, rollback and concurrency");
 }
}
