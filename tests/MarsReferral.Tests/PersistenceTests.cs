using MarsReferral.Core;
using System.Text.Json;

internal static class PersistenceTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MarsPersistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "data.json");
        static string State(ReferralService s) => JsonSerializer.Serialize(s.Read());
        static void Check(bool condition) { if (!condition) throw new Exception("Persistence assertion failed."); }
        var ops = new Actor(IsOperations: true);
        try
        {
            try { using var missing = new ReferralService(path); throw new Exception("Expected missing file rejection"); }
            catch (InvalidOperationException) { }
            Check(!File.Exists(path));
            File.WriteAllText(path, JsonSerializer.Serialize(new Snapshot([], [], [], [], [])));
            string expected;
            int addedId;
            using (var s = new ReferralService(path))
            {
                Check(s.Read().Customers.Length == 0 && File.Exists(path));
                var referrer = s.Register("Test Referrer", "referrer@example.com", null);
                ReferralTestSetup.Fund(s, referrer.Id);
                s.Refer(new(referrer.Id), "Direct Friend", "direct@example.com", "+1 416 555 0123");
                var c = s.Register("Persistent Customer", "persist@example.com", null, true);
                addedId = c.Id;
                s.CompleteOnboarding(new(c.Id), c.Id, true);
                s.Attach(new(c.Id), c.Id, s.Read().Customers[0].Code);
                s.ToggleCode(ops, c.Id);
                s.RecordShadow(ops, c.Id, true);
                var application = s.CreateApplication(new(c.Id, IsShadow: true), c.Id, "Refinance", 5000);
                s.ApproveAmount(ops, application.Id, 4000);
                s.Transition(ops, application.Id, ApplicationStage.Funded);
                foreach (var reward in s.Read().Rewards.Where(x => x.ApplicationId == application.Id))
                {
                    s.Review(ops, reward.Id, true, "");
                    s.Pay(ops, reward.Id, "PERSIST-" + reward.Id, false);
                    s.Pay(ops, reward.Id, "PERSIST-" + reward.Id, true);
                }
                s.RecordShadow(ops, c.Id, false);
                expected = State(s);
                var contents = File.ReadAllText(path);
                try { s.Register("Duplicate", "persist@example.com", null); throw new Exception("Expected rejection"); }
                catch (RuleException) { }
                Check(State(s) == expected && File.ReadAllText(path) == contents);
                try { using var duplicate = new ReferralService(path); throw new Exception("Expected exclusive lock"); }
                catch (InvalidOperationException) { }
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try { s.Register("Unsaved Customer", "unsaved@example.com", null); throw new Exception("Expected save failure"); }
                    catch (RuleException) { }
                    Check(State(s) == expected);
                }
                Check(File.ReadAllText(path) == contents);
            }
            using (var s = new ReferralService(path))
            {
                Check(State(s) == expected);
                Check(s.Register("Next Customer", "next@example.com", null).Id == addedId + 1);
                Parallel.For(0, 12, i => s.Register("Concurrent " + i, $"parallel{i}@example.com", null));
                expected = State(s);
            }
            using (var s = new ReferralService(path)) Check(State(s) == expected);
            foreach (var invalid in new[] { "{invalid", "{}", "null", "{\"Customers\":null,\"Applications\":[],\"Rewards\":[],\"Messages\":[],\"Audit\":[]}" })
            {
                File.WriteAllText(path, invalid);
                try { using var s = new ReferralService(path); throw new Exception("Expected invalid data rejection"); }
                catch (InvalidOperationException) { }
                Check(File.ReadAllText(path) == invalid);
            }
            Console.WriteLine("PASS JSON persistence: complete workflow, restart, IDs, concurrent writes, failed-write rollback, invalid files and exclusive ownership");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
