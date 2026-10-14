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

public record Address
{
    public string Street { get; init; } = "";

    public string City { get; init; } = "Vienna";
}

// A configurable member of every kind settings can store (D19)
public record EveryKindSettings
{
    public string Text { get; init; } = "";

    public bool Flag { get; init; }

    public int Count { get; init; }

    public long Big { get; init; }

    public float Ratio { get; init; }

    public double Precise { get; init; }

    public decimal Money { get; init; }

    public Guid Key { get; init; }

    public DateTime When { get; init; }

    public DateTimeOffset At { get; init; }

    public TimeSpan Span { get; init; }

    public DateOnly Day { get; init; }

    public TimeOnly Time { get; init; }

    public Uri? Link { get; init; }

    public Shade Shade { get; init; }

    public Days Days { get; init; }

    public Address Address { get; init; } = new();

    public IReadOnlyList<string> Tags { get; init; } = [];

    public int[] Numbers { get; init; } = [];

    public HashSet<Shade> Shades { get; init; } = [];

    public IReadOnlyDictionary<string, int> Scores { get; init; } = new Dictionary<string, int>();

    public Dictionary<Shade, Address> Places { get; init; } = [];

    public List<Address> History { get; init; } = [];
}

public partial class EveryKindToolset : Toolset
{
    public EveryKindToolset()
    {
        AddTool(Inspect)
            .WithConfigurableSettings<EveryKindSettings>();
    }

    /// <summary>
    /// Returns the settings it ran with.
    /// </summary>
    public static EveryKindSettings Inspect(EveryKindSettings settings) => settings;
}

public partial class EveryKindAgent : Agent
{
    public EveryKindAgent()
    {
        WithStorageKind("every_kind");

        WithSettingsVersion(2);

        WithChatOptions()
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Temperature)
            .Configurable(o => o.StopSequences)
            .Configurable(o => o.Reasoning);

        UseToolset<EveryKindToolset>();
    }
}
