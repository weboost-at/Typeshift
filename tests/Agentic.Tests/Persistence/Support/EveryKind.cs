using Agentic.Persistence;

namespace Agentic.Tests.Persistence.Support;

public enum Shade
{
    Light,
    Dark
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

    public DateTimeOffset? At { get; init; }

    public TimeSpan? Span { get; init; }

    public Shade? Shade { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public string Serialize() =>
        StorableJson.Write(writer =>
        {
            StorableJson.Write(writer, "text", Text);
            StorableJson.Write(writer, "flag", Flag);
            StorableJson.Write(writer, "count", Count);
            StorableJson.Write(writer, "big", Big);
            StorableJson.Write(writer, "ratio", Ratio);
            StorableJson.Write(writer, "precise", Precise);
            StorableJson.Write(writer, "money", Money);
            StorableJson.Write(writer, "key", Key);
            StorableJson.Write(writer, "at", At);
            StorableJson.Write(writer, "span", Span);
            StorableJson.WriteEnum(writer, "shade", Shade);
            StorableJson.Write(writer, "tags", Tags);
        });

    public static EveryKind Deserialize(string json) =>
        StorableJson.Read(json, static data => new EveryKind
        {
            Text = StorableJson.ReadString(data, "text"),
            Flag = StorableJson.ReadBoolean(data, "flag"),
            Count = StorableJson.ReadInt32(data, "count"),
            Big = StorableJson.ReadInt64(data, "big"),
            Ratio = StorableJson.ReadSingle(data, "ratio"),
            Precise = StorableJson.ReadDouble(data, "precise"),
            Money = StorableJson.ReadDecimal(data, "money"),
            Key = StorableJson.ReadGuid(data, "key"),
            At = StorableJson.ReadDateTimeOffset(data, "at"),
            Span = StorableJson.ReadTimeSpan(data, "span"),
            Shade = StorableJson.ReadEnum<Shade>(data, "shade"),
            Tags = StorableJson.ReadStrings(data, "tags")
        });
}
