using System.ComponentModel.DataAnnotations;
using MarsReferral.Core;
namespace MarsReferral.Web.Models;
public class ShareInput {
    [Range(1,int.MaxValue)] public int CustomerId {get;set;}
    [Required,StringLength(80,MinimumLength=2)] public string Recipient {get;set;} = "Sophie Campbell";
    [Required(ErrorMessage="Enter the recipient contact for the selected channel."),StringLength(250)] public string Contact {get;set;} = "";
    [Required] public string Channel {get;set;} = "WhatsApp";
    [Required,StringLength(1200,MinimumLength=10)] public string Body {get;set;} = "Hi! Discover how a reverse mortgage could help you make the most of your home. Join through my referral link: you could receive $25 and I could receive $50 after qualifying funding and approval. This is a HomeEquity Bank referral POC demo.";
}
public record SharingModel(PortalModel Portal,ShareInput Input,Invitation? Active);
public class EnquiryInput {
    [Required] public string Code {get;set;} = "";
    public string? InvitationId {get;set;}
    [Required,StringLength(80,MinimumLength=2)] public string Name {get;set;} = "";
    [Required,EmailAddress,StringLength(160)] public string Email {get;set;} = "";
    [Required,StringLength(30)] public string Phone {get;set;} = "";
    [Required] public string Interest {get;set;} = "Reverse mortgage";
    public bool Consent {get;set;}
}
public record ReferralLanding(Customer Owner,EnquiryInput Input);
public class PayInput {
    [Range(1,int.MaxValue)] public int Id {get;set;}
    [Required,StringLength(40,MinimumLength=4),RegularExpression(@"^[A-Za-z0-9\-]{4,40}$")] public string Reference {get;set;} = "";
    [Required] public bool? Successful {get;set;}
}
