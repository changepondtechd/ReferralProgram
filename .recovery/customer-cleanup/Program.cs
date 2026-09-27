using System.Text.Json;
using System.Text.Json.Nodes;
using MarsReferral.Core;

var path = Path.GetFullPath(args.Single());
using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
var original = File.ReadAllBytes(path);
var data = JsonNode.Parse(original)!.AsObject();
var customer = data["Customers"]!.AsArray().First()!.DeepClone();
var id = customer["Id"]!.GetValue<int>();
if (id != 1 || customer["Email"]!.GetValue<string>() != "liam.thompson@example.com")
    throw new InvalidOperationException("First customer changed; review before cleanup.");
if (customer["ReferrerId"] != null)
{
    customer["ReferrerId"] = null;
    customer["AttachedAt"] = null;
}
var countsBefore = new Dictionary<string, int>();
foreach (var key in new[] { "Customers", "Applications", "Rewards", "Messages", "Audit", "Invitations", "Enquiries" })
    countsBefore[key] = data[key]!.AsArray().Count;
void Keep(string key, Func<JsonNode, bool> predicate)
{
    data[key] = new JsonArray(data[key]!.AsArray().Where(x => predicate(x!)).Select(x => x!.DeepClone()).ToArray());
}
int Number(JsonNode row, string key) => row[key]!.GetValue<int>();
data["Customers"] = new JsonArray(customer);
Keep("Applications", x => Number(x, "CustomerId") == id);
var applicationIds = data["Applications"]!.AsArray().Select(x => Number(x!, "Id")).ToHashSet();
Keep("Rewards", x => Number(x, "BeneficiaryId") == id && Number(x, "ReferredId") == id && applicationIds.Contains(Number(x, "ApplicationId")));
Keep("Messages", x => Number(x, "CustomerId") == id);
Keep("Audit", x => x["CustomerId"] == null || Number(x, "CustomerId") == id);
Keep("Invitations", x => Number(x, "ReferrerId") == id);
var invitationIds = data["Invitations"]!.AsArray().Select(x => x!["Id"]!.GetValue<string>()).ToHashSet();
Keep("Enquiries", x => Number(x, "ReferrerId") == id && (x["InvitationId"] == null || invitationIds.Contains(x["InvitationId"]!.GetValue<string>())));
var token = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
var backupDirectory = Path.Combine(Path.GetDirectoryName(path)!, "..", "..", "..", ".recovery", "customer-cleanup", token);
Directory.CreateDirectory(backupDirectory);
var backup = Path.GetFullPath(Path.Combine(backupDirectory, "referral-data.before.json"));
var staged = path + ".cleanup-" + token + ".json";
try
{
    File.WriteAllBytes(backup, original);
    if (!File.ReadAllBytes(backup).SequenceEqual(original)) throw new IOException("Backup verification failed.");
    using (var originalCheck = new ReferralService(backup)) { }
    File.WriteAllText(staged, data.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    using (var validation = new ReferralService(staged))
    {
        var snapshot = validation.Read();
        if (snapshot.Customers.Length != 1 || snapshot.Customers[0].Id != id)
            throw new InvalidDataException("Unexpected surviving customers.");
        validation.RefreshPresentationDemo();
        validation.SeedDemoEnquiries();
    }
    var result = File.ReadAllBytes(staged);
    if (!File.ReadAllBytes(path).SequenceEqual(original)) throw new IOException("Live data changed during cleanup.");
    File.Replace(staged, path, null);
    if (!File.ReadAllBytes(path).SequenceEqual(result)) throw new IOException("Replacement verification failed.");
    Console.WriteLine($"Kept: {customer["Name"]} (ID {id})");
    foreach (var entry in countsBefore)
        Console.WriteLine($"{entry.Key}: {entry.Value} -> {data[entry.Key]!.AsArray().Count}");
    Console.WriteLine($"Backup: {backup}");
    Console.WriteLine("Validated using ReferralService, including startup refresh and enquiry seeding.");
}
finally
{
    if (File.Exists(staged)) File.Delete(staged);
}
