using Stay.Core;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Stay;

/// <summary>Installed Store-style app packages, through the same API Settings uses.</summary>
public sealed class WinPackages : IPackages
{
    private readonly PackageManager _manager = new();

    public IReadOnlyList<InstalledApp> List()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<InstalledApp>();
        foreach (var package in _manager.FindPackagesForUser(""))
        {
            string family;
            try { family = package.Id.FamilyName; } catch { continue; }
            if (!seen.Add(family)) continue;

            // Every one of these can throw on a package whose manifest will not read, and one bad package must
            // not empty the whole list.
            string display = ""; try { display = package.DisplayName; } catch { }
            bool system = false; try { system = package.SignatureKind == PackageSignatureKind.System; } catch { }
            bool framework = false; try { framework = package.IsFramework; } catch { }
            string version = "";
            try { var v = package.Id.Version; version = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}"; } catch { }

            list.Add(new InstalledApp(family, package.Id.Name, package.Id.FullName, display,
                                      Publisher(package), version, framework, system));
        }
        return list;
    }

    private static string Publisher(Package package)
    {
        try
        {
            var name = package.Id.Publisher ?? "";
            int at = name.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return name;
            var rest = name[(at + 3)..];
            int comma = rest.IndexOf(',');
            return comma < 0 ? rest : rest[..comma];
        }
        catch { return ""; }
    }

    public void Remove(InstalledApp app)
    {
        var result = _manager.RemovePackageAsync(app.FullName, RemovalOptions.RemoveForAllUsers).AsTask();
        result.Wait(TimeSpan.FromMinutes(3));
        if (result.Result.IsRegistered) throw new InvalidOperationException(
            result.Result.ErrorText.Length > 0 ? result.Result.ErrorText : "Windows would not remove it");
    }
}
