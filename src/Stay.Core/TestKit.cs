using System.Text;

namespace Stay.Core;

/// <summary>A tiny test harness so the app can check itself with "stay selftest". No framework, no network, no real registry.</summary>
public sealed class Suite
{
    public string Name { get; }
    public List<(string Check, bool Ok, string Detail)> Results { get; } = new();
    public Suite(string name) => Name = name;

    public void Check(string name, bool ok, string detail = "")
        => Results.Add((name, ok, detail));

    public void Equal<T>(string name, T expected, T actual)
        => Check(name, Equals(expected, actual), Equals(expected, actual) ? "" : $"expected {expected}, got {actual}");

    public void Throws<TEx>(string name, Action act) where TEx : Exception
    {
        try { act(); Check(name, false, "did not throw"); }
        catch (TEx) { Check(name, true); }
        catch (Exception ex) { Check(name, false, $"threw {ex.GetType().Name}: {ex.Message}"); }
    }

    public int Failed => Results.Count(r => !r.Ok);
}

public static class SelfTest
{
    public static IReadOnlyList<Func<Suite>> All => new Func<Suite>[]
    {
        Tests.LifecycleSuite.Run,
        Tests.StandingSuite.Run,
        Tests.ElevenSuite.Run,
        Tests.GuardsSuite.Run,
        Tests.EngineSuite.Run,
        Tests.ReceiptSuite.Run,
    };

    /// <summary>Runs every suite; returns the number of failed checks. Output is one line per check, last line the totals.</summary>
    public static int Run(TextWriter output, string? filter = null, bool list = false)
    {
        int passed = 0, failed = 0;
        foreach (var run in All)
        {
            var suite = run();
            if (filter is not null && !suite.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var (check, ok, detail) in suite.Results)
            {
                if (list) { output.WriteLine($"{suite.Name}: {check}"); continue; }
                if (ok) passed++; else failed++;
                output.WriteLine($"{(ok ? "ok  " : "FAIL")} {suite.Name}: {check}{(detail.Length > 0 ? " (" + detail + ")" : "")}");
            }
        }
        if (!list) output.WriteLine($"{passed} passed, {failed} failed");
        return failed;
    }
}
