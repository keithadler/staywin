using Microsoft.Win32;
using Stay.Core;

namespace Stay;

/// <summary>The real registry. Only the values the catalogue names are ever read or written.</summary>
public sealed class WinRegistry : IRegistry
{
    private static (RegistryKey Root, string Key) Resolve(Hive hive, string key) => hive switch
    {
        Hive.LocalMachine => (Registry.LocalMachine, key),
        Hive.NetworkService => (Registry.Users, @"S-1-5-20\" + key),
        _ => (Registry.CurrentUser, key),
    };

    public RegValue Read(Hive hive, string key, string name)
    {
        var (root, path) = Resolve(hive, key);
        using var k = root.OpenSubKey(path, writable: false);
        if (k is null) return RegValue.Absent;
        var v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return v switch
        {
            null => RegValue.Absent,
            int i => RegValue.DWord(unchecked((uint)i)),
            long l => RegValue.DWord(l),
            string s => RegValue.Str(s),
            byte[] b => RegValue.Bytes(b),
            _ => RegValue.Str(v.ToString() ?? ""),
        };
    }

    public void Write(Hive hive, string key, string name, RegValue value)
    {
        if (value.Kind == ValueKind.Absent)
        {
            var (deleteRoot, deletePath) = Resolve(hive, key);
            using var existing = deleteRoot.OpenSubKey(deletePath, writable: true);
            existing?.DeleteValue(name, throwOnMissingValue: false);
            return;
        }
        var (root, path) = Resolve(hive, key);
        using var k = root.CreateSubKey(path, writable: true)
            ?? throw new InvalidOperationException($"cannot open {RegEdit.HiveName(hive)}\\{key}");
        if (value.Kind == ValueKind.DWord) k.SetValue(name, unchecked((int)(uint)value.Number), RegistryValueKind.DWord);
        else if (value.Kind == ValueKind.Binary) k.SetValue(name, value.AsBytes(), RegistryValueKind.Binary);
        else k.SetValue(name, value.Text, RegistryValueKind.String);
    }

    public IReadOnlyList<string> SubKeys(Hive hive, string key)
    {
        try
        {
            var (root, path) = Resolve(hive, key);
            using var k = root.OpenSubKey(path, writable: false);
            return k?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }
}
