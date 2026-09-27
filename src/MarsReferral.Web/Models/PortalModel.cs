using MarsReferral.Core;
using System.ComponentModel.DataAnnotations;
namespace MarsReferral.Web.Models;
public record PortalModel(Snapshot Data, Actor Actor, string Query = "", string Status = "")
{
    public Pagination Pagination { get; init; } = new(0, 1, 10);
    public bool IsOperations => Actor.IsOperations;
    public Customer? Current => Data.Customers.SingleOrDefault(x => x.Id == Actor.CustomerId);
    public string Name(int id) => Data.Customers.SingleOrDefault(x => x.Id == id)?.Name ?? "Customer";
    public string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0]));
}
public class RegisterInput
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = "";
    [StringLength(16)] public string? ReferralCode { get; set; }
}