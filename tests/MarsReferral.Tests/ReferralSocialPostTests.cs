using MarsReferral.Core;
using MarsReferral.Web.Controllers;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;

internal static class ReferralSocialPostTests
{
 public static void Run()
 {
  static void Check(bool ok,string message){if(!ok)throw new Exception("Social post assertion failed: "+message);}
  using var service=new ReferralService();
  var owner=service.Register("Social Owner","social-owner@example.com",null);
  var other=service.Register("Other Customer","social-other@example.com",null);
  var pending=service.Register("Pending Customer","social-pending@example.com",null,onboardingRequired:true);
  var context=new DefaultHttpContext();
  context.Request.Scheme="https";context.Request.Host=new HostString("referrals.example.com",8443);context.Request.PathBase="/portal";
  context.Request.QueryString=new QueryString($"?code={other.Code}&customerId={other.Id}");
  var urls=new ReferralUrlHelper(new ActionContext{HttpContext=context});
  PortalModel Portal(int id)=>new(service.Read(),new Actor(id));
  var post=ReferralSocialPost.Create(Portal(owner.Id),urls);
  Check(post!=null,"completed onboarding and an active code allow sharing without a funded file");
  var expected=$"https://referrals.example.com:8443/portal/referral?code={owner.Code}";
  Check(post!.ReferralUrl==expected,"absolute link retains host, port, path base and current customer's code");
  Check(!post.IsLocalAddress,"public host is not loopback");
  Check(post.Text.EndsWith(expected) && post.Text.Contains("CHIP Reverse Mortgage") && post.Text.Contains("55+") && post.Text.Contains("Customer-to-Customer"),"product, referral scheme and registration link are included");
  Check(post.Text.Contains("$25 CAD") && post.Text.Contains("$50 CAD") && post.Text.Contains("qualifying funding and approval") && post.Text.Contains("not an approved HomeEquity Bank offer"),"rewards retain conditions and demo disclaimer");
  Check(post.TwitterText.Length+1+23<=280 && post.TwitterText.Contains("POC demo") && post.TwitterText.Contains("Eligibility and terms apply"),"X post fits the standard limit including its shortened URL and retains caveats");
  foreach(var item in new[]{(post.LinkedInUrl,"www.linkedin.com","/sharing/share-offsite/","url"),(post.TwitterUrl,"twitter.com","/intent/tweet","url"),(post.FacebookUrl,"www.facebook.com","/sharer/sharer.php","u")})
  {
   var uri=new Uri(item.Item1);
   Check(uri.Scheme=="https" && uri.Host==item.Item2 && uri.AbsolutePath==item.Item3,"expected HTTPS platform composer");
   Check(QueryHelpers.ParseQuery(uri.Query)[item.Item4]==expected,"platform receives the intact referral URL");
  }
  Check(QueryHelpers.ParseQuery(new Uri(post.TwitterUrl).Query)["text"]==post.TwitterText,"X receives the short post");
  var special=new ReferralSocialPost("https://example.com/portal/referral?code=ABC123&source=a%20b%2Bc#register");
  Check(QueryHelpers.ParseQuery(new Uri(special.LinkedInUrl).Query)["url"]==special.ReferralUrl && QueryHelpers.ParseQuery(new Uri(special.FacebookUrl).Query)["u"]==special.ReferralUrl && QueryHelpers.ParseQuery(new Uri(special.TwitterUrl).Query)["url"]==special.ReferralUrl,"nested URL encoding round-trips reserved characters");
  Check(service.Read().Invitations.Length==0 && service.Read().Customers.Length==3,"composing posts creates no invitations or accounts");

  var code=QueryHelpers.ParseQuery(new Uri(post.ReferralUrl).Query)["code"].ToString();
  var landing=new ReferralController(service){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};
  Check(landing.Index(code,null) is ViewResult page && page.Model is ReferralLanding model && model.Owner.Id==owner.Id && model.Input.Code==owner.Code && model.Input.Name=="","public form retains code and asks for recipient details");
  foreach(var name in new[]{"First","Second"})
  {
   var result=service.SubmitEnquiryAndRegister(code,null,name+" Friend",name.ToLowerInvariant()+"-social@example.com","+1 416 555 0178","Reverse mortgage",true);
   Check(result.AccountCreated && result.Customer.ReferrerId==owner.Id && result.Enquiry.ReferrerId==owner.Id,"multiple social visitors register with the post owner's code");
  }

  Check(ReferralSocialPost.Create(Portal(pending.Id),urls)==null,"incomplete onboarding cannot share");
  Check(ReferralSocialPost.Create(Portal(999999),urls)==null,"missing customer cannot share");
  Check(ReferralSocialPost.Create(new(service.Read(),new Actor(owner.Id,IsOperations:true)),urls)==null,"operations view cannot expose a customer post");
  urls.ReturnNull=true;Check(ReferralSocialPost.Create(Portal(owner.Id),urls)==null,"route failure does not produce broken share links");urls.ReturnNull=false;
  context.Request.Host=new HostString("localhost",5000);
  Check(ReferralSocialPost.Create(Portal(owner.Id),urls)!.IsLocalAddress,"localhost warning is enabled");
  Check(new ReferralSocialPost("http://127.0.0.1/referral?code=ABC123").IsLocalAddress,"loopback IP warning is enabled");
  service.ToggleCode(new Actor(IsOperations:true),owner.Id);
  Check(ReferralSocialPost.Create(Portal(owner.Id),urls)==null,"paused code cannot share");
  Check(landing.Index(code,null) is ViewResult invalid && invalid.ViewName=="Invalid" && landing.Response.StatusCode==400,"previously shared links respect paused codes");
  Console.WriteLine("PASS Social posts: platform URLs, encoding, current customer, demo wording, X length, registration attribution, eligibility and localhost warning");
 }

 private sealed class ReferralUrlHelper(ActionContext context) : IUrlHelper
 {
  public ActionContext ActionContext { get; }=context;
  public bool ReturnNull { get; set; }
  public string? Action(UrlActionContext actionContext)
  {
   if(actionContext.Action!="Index" || actionContext.Controller!="Referral")throw new Exception("Social posts must use the public referral action");
   if(ReturnNull)return null;
   var values=new RouteValueDictionary(actionContext.Values);
   return $"{actionContext.Protocol}://{actionContext.Host}{ActionContext.HttpContext.Request.PathBase}/referral?code={Uri.EscapeDataString(values["code"]?.ToString() ?? "")}";
  }
  public string? Content(string? contentPath)=>throw new NotSupportedException();
  public bool IsLocalUrl(string? url)=>throw new NotSupportedException();
  public string? Link(string? routeName,object? values)=>throw new NotSupportedException();
  public string? RouteUrl(UrlRouteContext routeContext)=>throw new NotSupportedException();
 }
}
