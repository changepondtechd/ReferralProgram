using System.Reflection;
using System.Security.Claims;
using MarsReferral.Core;
using MarsReferral.Web.Controllers;
using MarsReferral.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using QRCoder;

internal static class ReferralQrTests
{
 public static void Run()
 {
  static void Check(bool ok){if(!ok)throw new Exception("Referral QR assertion failed");}
  using var service=new ReferralService();
  var owner=service.Register("Liam Thompson","qr-owner@example.com",null);
  var other=service.Register("Olivia Martin","qr-other@example.com",null);
  var pending=service.Register("Pending Customer","qr-pending@example.com",null,onboardingRequired:true);
  var context=new DefaultHttpContext();
  context.Request.Scheme="https";context.Request.Host=new HostString("referrals.example.com",8443);context.Request.PathBase="/portal";
  context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,owner.Id.ToString()),new Claim(ClaimTypes.Role,"Customer")},"test"));
  context.Request.QueryString=new QueryString($"?code={other.Code}&customerId={other.Id}");
  var controller=new CustomerController(service){ControllerContext=new ControllerContext{HttpContext=context}};
  var urls=new ReferralUrlHelper(controller.ControllerContext);controller.Url=urls;
  var before=service.Read();
  var image=controller.ReferralQr() as FileContentResult;
  Check(image!=null && image.ContentType=="image/png" && string.IsNullOrEmpty(image.FileDownloadName));
  Check(image!.FileContents.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}));
  var expected=$"https://referrals.example.com:8443/portal/referral?code={owner.Code}";
  Check(urls.LastUrl==expected);
  using(var data=QRCodeGenerator.GenerateQrCode(expected,QRCodeGenerator.ECCLevel.Q))
  using(var qr=new PngByteQRCode(data))Check(image.FileContents.SequenceEqual(qr.GetGraphic(8)));
  var download=controller.ReferralQr(true) as FileContentResult;
  Check(download!=null && download.FileDownloadName=="referral-qr.png" && download.ContentType=="image/png" && download.FileContents.SequenceEqual(image.FileContents));
  Check(service.Read().Customers.Length==before.Customers.Length && service.Read().Invitations.Length==before.Invitations.Length);
  var action=typeof(CustomerController).GetMethod(nameof(CustomerController.ReferralQr))!;
  var cache=action.GetCustomAttribute<ResponseCacheAttribute>();
  Check(cache?.NoStore==true && cache.Location==ResponseCacheLocation.None);
  Check(typeof(CustomerController).GetCustomAttribute<AuthorizeAttribute>()?.Roles=="Customer" && action.GetCustomAttribute<AllowAnonymousAttribute>()==null);

  var code=QueryHelpers.ParseQuery(new Uri(expected).Query)["code"].ToString();
  var landing=new ReferralController(service){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};
  var page=landing.Index(code,null) as ViewResult;
  Check(page?.Model is ReferralLanding model && model.Owner.Id==owner.Id && model.Input.Code==owner.Code && string.IsNullOrEmpty(model.Input.Name));
  var result=service.SubmitEnquiryAndRegister(code,null,"QR Friend","qr-friend@example.com","+1 416 555 0178","General enquiry",true);
  Check(result.AccountCreated && result.Customer.ReferrerId==owner.Id && result.Enquiry.ReferrerId==owner.Id);
  Check(result.Customer.Name=="QR Friend" && result.Customer.Email=="qr-friend@example.com");

  urls.ReturnNull=true;Check(controller.ReferralQr() is StatusCodeResult failure && failure.StatusCode==500);urls.ReturnNull=false;
  service.ToggleCode(new Actor(IsOperations:true),owner.Id);
  Check(controller.ReferralQr() is BadRequestObjectResult && controller.ReferralQr(true) is BadRequestObjectResult);
  Check(landing.Index(code,null) is ViewResult invalid && invalid.ViewName=="Invalid" && landing.Response.StatusCode==400);
  context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,pending.Id.ToString())},"test"));
  Check(controller.ReferralQr() is NotFoundResult);
  context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"999999")},"test"));
  Check(controller.ReferralQr() is NotFoundResult);
  Console.WriteLine("PASS Referral QR: current-customer payload, absolute URL, PNG/download, no-store/auth metadata, recipient details and attachment, paused and incomplete accounts");
 }

 private sealed class ReferralUrlHelper(ActionContext context) : IUrlHelper
 {
  public ActionContext ActionContext { get; }=context;
  public string? LastUrl { get; private set; }
  public bool ReturnNull { get; set; }
  public string? Action(UrlActionContext actionContext)
  {
   if(actionContext.Action!="Index" || actionContext.Controller!="Referral")throw new Exception("QR must use the public referral action");
   if(ReturnNull)return null;
   var values=new RouteValueDictionary(actionContext.Values);
   LastUrl=$"{actionContext.Protocol}://{actionContext.Host}{ActionContext.HttpContext.Request.PathBase}/referral?code={Uri.EscapeDataString(values["code"]?.ToString() ?? "")}";
   return LastUrl;
  }
  public string? Content(string? contentPath)=>throw new NotSupportedException();
  public bool IsLocalUrl(string? url)=>throw new NotSupportedException();
  public string? Link(string? routeName,object? values)=>throw new NotSupportedException();
  public string? RouteUrl(UrlRouteContext routeContext)=>throw new NotSupportedException();
 }
}
