using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using MarsReferral.Core;
namespace MarsReferral.Web.Models;
public static class PersonNames
{
    public static IHtmlContent Bold(string? text, Snapshot data) => Bold(text,
        data.Customers.Select(x=>x.Name).Concat(data.Enquiries.Select(x=>x.Name))
        .Concat(data.Invitations.Select(x=>x.Recipient)).Concat(new[]{"Sarah Mitchell", "Rajni Kaushal", "Kuldip Solanki", "Sushmitha Amaran", "V Karim"}));
    public static IHtmlContent Bold(string? text, IEnumerable<string> names)
    {
        var output = new HtmlContentBuilder(); text ??= "";
        var choices=names.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x=>x.Length).Select(Regex.Escape).ToArray();
        if(choices.Length==0)return output.Append(text);
        var pattern=@"(?<![\p{L}\p{N}])(?:"+string.Join("|",choices)+@")(?![\p{L}\p{N}])";
        var offset=0;
        foreach(Match m in Regex.Matches(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1))){
            output.Append(text[offset..m.Index]).AppendHtml("<strong class=\"person-name\">").Append(m.Value).AppendHtml("</strong>");offset=m.Index+m.Length;
        }
        return output.Append(text[offset..]);
    }
}
