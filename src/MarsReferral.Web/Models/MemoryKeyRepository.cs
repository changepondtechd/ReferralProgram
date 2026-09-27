using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
namespace MarsReferral.Web.Models;

// .NET's key warm-up service also needs a process-local repository, even when
// the cookie provider is ephemeral. No user-machine key files are touched.
public sealed class MemoryKeyRepository : IXmlRepository
{
    private readonly List<XElement> elements = [];
    private readonly object gate = new();
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (gate) return elements.Select(x => new XElement(x)).ToArray();
    }
    public void StoreElement(XElement element, string friendlyName)
    {
        lock (gate) elements.Add(new XElement(element));
    }
}
