using System.Diagnostics;
using System.IO;
using Stay.Core;

namespace Stay;

/// <summary>Where the app keeps its receipts. STAY_HOME overrides it, so tests never touch the real folder.</summary>
public static class Paths
{
    public static string Home => Environment.GetEnvironmentVariable("STAY_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Stay for Windows 10");
    public static string Receipts => Path.Combine(Home, "receipts");
    public static string Log => Path.Combine(Home, "stay.log");
}

public sealed class FileReceiptStore : IReceiptStore
{
    public void Save(Receipt receipt)
    {
        Directory.CreateDirectory(Paths.Receipts);
        File.WriteAllText(Path.Combine(Paths.Receipts, receipt.Id + ".json"), receipt.ToJson());
    }

    public IReadOnlyList<Receipt> List()
    {
        if (!Directory.Exists(Paths.Receipts)) return Array.Empty<Receipt>();
        var list = new List<Receipt>();
        foreach (var f in Directory.GetFiles(Paths.Receipts, "*.json"))
        {
            try { list.Add(Receipt.FromJson(File.ReadAllText(f))); }
            catch { /* a receipt we cannot read is left alone, never deleted */ }
        }
        return list.OrderByDescending(r => r.When).ToList();
    }
}

public static class RestorePoint
{
    /// <summary>Asks Windows for a restore point. Returns false, with a reason, when System Protection is off or Windows refuses.</summary>
    public static (bool Ok, string Reason) Create()
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'Stay for Windows 10' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; 'ok'\"")
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(120_000);
            if (stdout.Contains("ok")) return (true, "");
            var reason = stderr.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "Windows did not make one";
            return (false, reason);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
