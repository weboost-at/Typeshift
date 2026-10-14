using Typeshift.Persistence;

namespace Typeshift.Tests.Persistence.Support;

public enum Shade
{
    Light,
    Dark
}

[Flags]
public enum Days
{
    None = 0,
    Monday = 1,
    Tuesday = 2
}

// Written by hand as the generator writes storable data, with a member of every kind settings can store
public sealed record EveryKind : IStorable<EveryKind>
{
    public string? Text { get; init; }

    public bool? Flag { get; init; }

    public int? Count { get; init; }

    public long? Big { get; init; }

    public float? Ratio { get; init; }

    public double? Precise { get; init; }

    public decimal? Money { get; init; }

    public Guid? Key { get; init; }

    public DateTime? When { get; init; }

    public DateTimeOffset? At { get; init; }

    public TimeSpan? Span { get; init; }

    public Shade? Shade { get; init; }

    public Days? Days { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public string Serialize() =>
        StorableJson.Write(writer =>
        {
            StorableJson.Write(writer, "text", Text);
            StorableJson.Write(writer, "flag", Flag);
            StorableJson.WriteNumber(writer, "count", Count);
            StorableJson.WriteNumber(writer, "big", Big);
            StorableJson.WriteNumber(writer, "ratio", Ratio);
            StorableJson.WriteNumber(writer, "precise", Precise);
            StorableJson.WriteNumber(writer, "money", Money);
            StorableJson.Write(writer, "key", Key);
            StorableJson.Write(writer, "when", When);
            StorableJson.Write(writer, "at", At);
            StorableJson.Write(writer, "span", Span);
            StorableJson.WriteEnum(writer, "shade", Shade);
            StorableJson.WriteEnum(writer, "days", Days);
            StorableJson.Write(writer, "tags", Tags);
        });

    public static EveryKind Deserialize(string json) =>
        StorableJson.Read(json, static data => new EveryKind
        {
            Text = StorableJson.ReadString(data, "text"),
            Flag = StorableJson.ReadBoolean(data, "flag"),
            Count = StorableJson.ReadNumber<int>(data, "count"),
            Big = StorableJson.ReadNumber<long>(data, "big"),
            Ratio = StorableJson.ReadNumber<float>(data, "ratio"),
            Precise = StorableJson.ReadNumber<double>(data, "precise"),
            Money = StorableJson.ReadNumber<decimal>(data, "money"),
            Key = StorableJson.ReadGuid(data, "key"),
            When = StorableJson.ReadDateTime(data, "when"),
            At = StorableJson.ReadDateTimeOffset(data, "at"),
            Span = StorableJson.ReadTimeSpan(data, "span"),
            Shade = StorableJson.ReadEnum<Shade>(data, "shade"),
            Days = StorableJson.ReadEnum<Days>(data, "days"),
            Tags = StorableJson.ReadStrings(data, "tags")
        });
}
