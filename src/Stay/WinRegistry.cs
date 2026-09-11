using Microsoft.Win32;
using Stay.Core;

namespace Stay;

/// <summary>The real registry. Only the values the catalogue names are ever read or written.</summary>
public sealed class WinRegistry : IRegistry
{
    private static RegistryKey Root(Hive hive) => hive == Hive.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;

    public RegValue Read(Hive hive, string key, string name)
    {
        using var k = Root(hive).OpenSubKey(key, writable: false);
        if (k is null) return RegValue.Absent;
        var v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return v switch
        {
            null => RegValue.Absent,
            int i => RegValue.DWord(unchecked((uint)i)),
            long l => RegValue.DWord(l),
            string s => RegValue.Str(s),
            _ => RegValue.Str(v.ToString() ?? ""),
        };
    }

    public void Write(Hive hive, string key, string name, RegValue value)
    {
        if (value.Kind == ValueKind.Absent)
        {
            using var existing = Root(hive).OpenSubKey(key, writable: true);
            existing?.DeleteValue(name, throwOnMissingValue: false);
            return;
        }
        using var k = Root(hive).CreateSubKey(key, writable: true)
            ?? throw new InvalidOperationException($"cannot open {RegEdit.HiveName(hive)}\\{key}");
        if (value.Kind == ValueKind.DWord) k.SetValue(name, unchecked((int)(uint)value.Number), RegistryValueKind.DWord);
        else k.SetValue(name, value.Text, RegistryValueKind.String);
    }

    public IReadOnlyList<string> SubKeys(Hive hive, string key)
    {
        try
        {
            using var k = Root(hive).OpenSubKey(key, writable: false);
            return k?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }
}
