using System;
using System.IO;
using System.Text.Json;

internal static class Program
{
    static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "core-checks.json";
        var started = DateTime.UtcNow;
        int society = 0, auction = 0, settlement = 0;
        string failure = null;
        try { society = SocietyChecks.Validate(); auction = PortableAuctionChecks.Validate(); settlement = SettlementChecks.Validate(); }
        catch (Exception error) { failure = error.ToString(); }
        var report = new { passed = failure == null, society, auction, settlement, total = society + auction + settlement,
            seed = 22092026, startedUtc = started, elapsedMs = (DateTime.UtcNow - started).TotalMilliseconds,
            runtime = Environment.Version.ToString(), failure };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(failure == null ? $"PASS: {society} society + {auction} auction + {settlement} settlement checks ({society + auction + settlement} total)." : failure);
        return failure == null ? 0 : 1;
    }
}
