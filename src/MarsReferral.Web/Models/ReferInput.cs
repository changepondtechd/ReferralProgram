using System.ComponentModel.DataAnnotations;
namespace MarsReferral.Web.Models;
public class ReferInput
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = "";
    [Required, StringLength(30)] public string Phone { get; set; } = "";
}
