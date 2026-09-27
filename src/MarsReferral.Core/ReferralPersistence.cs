using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarsReferral.Core;

public partial class ReferralService : IDisposable
{
    private string? dataPath;
    private FileStream? writerLease;
    private int mutationDepth;
    private bool disposed;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // The parameterless constructor creates empty collections for isolated business tests.
    public ReferralService(string filePath) : this()
    {
        var path = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            // Hold an exclusive lease for the service lifetime: two hosts must not overwrite each other.
            writerLease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            if (File.Exists(path))
            {
                using var input = File.OpenRead(path);
                var snapshot = JsonSerializer.Deserialize<Snapshot>(input, JsonOptions)
                    ?? throw new InvalidDataException("The referral data file is empty.");
                ValidateSnapshot(snapshot);
                Restore(snapshot);
            }
            else
            {
                throw new FileNotFoundException("The referral JSON file is required. Restore it before starting the app.", path);
            }
            dataPath = path;

        }
        catch (Exception ex)
        {
            writerLease?.Dispose();
            throw new InvalidOperationException($"Cannot open referral data at '{path}'. Check file contents, permissions and whether another app is using it. Existing data has not been reset.", ex);
        }
    }

    private T Mutate<T>(Func<T> operation)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (mutationDepth > 0) return operation();
            var before = Read();
            mutationDepth++;
            try
            {
                var result = operation();
                Save();
                return result;
            }
            catch (Exception ex)
            {
                Restore(before);
                if (ex is IOException or UnauthorizedAccessException)
                    throw new RuleException("Your changes could not be saved. Check data-file access and disk space, then try again.");
                throw;
            }
            finally { mutationDepth--; }
        }
    }

    private void Mutate(Action operation) => Mutate(() => { operation(); return true; });

    private void Save()
    {
        if (dataPath == null) return;
        var temporary = dataPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, Read(), JsonOptions);
                output.Flush(flushToDisk: true);
            }
            // Replace on the same volume only after the complete new snapshot is flushed.
            if (File.Exists(dataPath)) File.Replace(temporary, dataPath, null);
            else File.Move(temporary, dataPath);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { /* A leftover temp file is never loaded as live data. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    private void Restore(Snapshot snapshot)
    {
        invitations.Clear(); invitations.AddRange(snapshot.Invitations);
        enquiries.Clear(); enquiries.AddRange(snapshot.Enquiries);
        presentationVersion = snapshot.PresentationVersion;
        customers.Clear(); customers.AddRange(snapshot.Customers);
        applications.Clear(); applications.AddRange(snapshot.Applications);
        rewards.Clear(); rewards.AddRange(snapshot.Rewards);
        messages.Clear(); messages.AddRange(snapshot.Messages);
        audit.Clear(); audit.AddRange(snapshot.Audit);
        nextCustomer = NextId(customers.Select(x => x.Id), 1);
        nextApplication = NextId(applications.Select(x => x.Id), 1001);
        nextReward = NextId(rewards.Select(x => x.Id), 2001);
        nextMessage = NextId(messages.Select(x => x.Id), 1);
    }

    private static int NextId(IEnumerable<int> ids, int minimum) => checked(Math.Max(minimum - 1, ids.DefaultIfEmpty(0).Max()) + 1);

    private static void ValidateSnapshot(Snapshot s)
    {
        static void Ensure([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool valid) { if (!valid) throw new InvalidDataException("Invalid or inconsistent referral data."); }
        static bool ValidIds(IEnumerable<int> source)
        {
            var ids = source.ToArray();
            return ids.All(x => x > 0 && x < int.MaxValue) && ids.Distinct().Count() == ids.Length;
        }
        Ensure(s.Customers != null && s.Applications != null && s.Rewards != null && s.Messages != null && s.Audit != null);
        Ensure(s.Customers!.All(x => x != null) && s.Applications!.All(x => x != null) && s.Rewards!.All(x => x != null) && s.Messages!.All(x => x != null) && s.Audit!.All(x => x != null));
        Ensure(ValidIds(s.Customers.Select(x => x.Id)) && ValidIds(s.Applications!.Select(x => x.Id)) && ValidIds(s.Rewards!.Select(x => x.Id)) && ValidIds(s.Messages!.Select(x => x.Id)));
        var customerIds = s.Customers.Select(x => x.Id).ToHashSet();
        Ensure(s.Invitations != null && s.Enquiries != null);
        Ensure(s.Invitations.All(x => x != null && customerIds.Contains(x.ReferrerId) && x.Id.Length == 32 && x.Channel is "WhatsApp" or "LinkedIn" && x.Status is "Sent" or "Delivered"));
        Ensure(s.Invitations.Select(x => x.Id).Distinct().Count() == s.Invitations.Length);
        Ensure(s.Enquiries.All(x => x != null && customerIds.Contains(x.ReferrerId) && x.Id.Length == 32 && x.Status is "New" or "Contacted" && (x.InvitationId == null || s.Invitations.Any(i => i.Id == x.InvitationId && i.ReferrerId == x.ReferrerId))));
        Ensure(s.Enquiries.Select(x => x.Id).Distinct().Count() == s.Enquiries.Length);
        var applicationIds = s.Applications!.Select(x => x.Id).ToHashSet();
        Ensure(s.Customers.All(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Email) && !string.IsNullOrWhiteSpace(x.Code) && (x.ReferrerId == null || (x.ReferrerId != x.Id && customerIds.Contains(x.ReferrerId.Value)))));
        Ensure(s.Customers.Select(x => x.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count() == s.Customers.Length);
        Ensure(s.Customers.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() == s.Customers.Length);
        foreach (var customer in s.Customers)
        {
            var visited = new HashSet<int> { customer.Id };
            var parent = customer.ReferrerId;
            while (parent is int id) { Ensure(visited.Add(id)); parent = s.Customers.Single(x => x.Id == id).ReferrerId; }
        }
        Ensure(s.Applications.All(x => customerIds.Contains(x.CustomerId) && Enum.IsDefined(x.Stage)));
        Ensure(s.Rewards!.All(x => customerIds.Contains(x.BeneficiaryId) && customerIds.Contains(x.ReferredId) && applicationIds.Contains(x.ApplicationId) && Enum.IsDefined(x.Stage)));
        Ensure(s.Messages!.All(x => customerIds.Contains(x.CustomerId) && x.Title != null && x.Body != null));
        Ensure(s.Audit!.All(x => x.Actor != null && x.Action != null && x.Detail != null && (x.CustomerId == null || customerIds.Contains(x.CustomerId.Value))));
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; writerLease?.Dispose(); }
    }
}
