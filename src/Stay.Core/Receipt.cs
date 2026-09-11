using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stay.Core;

/// <summary>One registry value the app changed, with what it was before so it can be put back.</summary>
public sealed record RegChange(
    string GuardId,
    Hive Hive,
    string Key,
    string Name,
    RegValue Before,
    RegValue After,
    string? Error = null,
    string? User = null,
    string? UserName = null)
{
    [JsonIgnore] public bool Failed => Error is not null;
    [JsonIgnore] public string Path => $"{(User is null ? RegEdit.HiveName(Hive) : "HKU(" + (UserName ?? User) + ")")}\\{Key}\\{Name}";
}

/// <summary>One app removed. The app cannot put it back; the Store link can, so the receipt keeps it.</summary>
public sealed record AppRemoval(
    string FamilyName,
    string Title,
    string StoreLink,
    string? Error = null)
{
    [JsonIgnore] public bool Failed => Error is not null;
}

/// <summary>Everything one press of Apply did, in the order it did it. Nothing is changed without one.</summary>
public sealed record Receipt(
    string Id,
    DateTimeOffset When,
    string Host,
    string User,
    string Version,
    IReadOnlyList<RegChange> Registry,
    IReadOnlyList<AppRemoval> Apps,
    bool RestorePoint,
    DateTimeOffset? Undone = null)
{
    [JsonIgnore] public int Changed => Registry.Count(c => !c.Failed) + Apps.Count(a => !a.Failed);
    [JsonIgnore] public int Failed => Registry.Count(c => c.Failed) + Apps.Count(a => a.Failed);

    /// <summary>Removals cannot be undone by putting a value back; the Store link is the only way.</summary>
    [JsonIgnore] public bool NeedsStore => Apps.Any(a => !a.Failed);

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static Receipt FromJson(string json) => JsonSerializer.Deserialize<Receipt>(json, Json)
        ?? throw new InvalidDataException("not a receipt");

    public static string NewId(DateTimeOffset when) => when.ToString("yyyyMMdd-HHmmss");
}
