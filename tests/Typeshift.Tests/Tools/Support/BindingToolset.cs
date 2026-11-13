namespace Typeshift.Tests.Tools.Support;

/// <param name="Street">The street and the house number.</param>
/// <param name="Zip">The postal code.</param>
public sealed record Destination(string Street, int Zip);

/// <param name="Id">The parcel's id.</param>
/// <param name="Destination">Where the parcel goes.</param>
public sealed record Parcel(string Id, Destination Destination);

public sealed record Refunded(int Number, string Reason);

public sealed record Reminder(int? Days, string? Note);

// Each tool hands back what it received, so a test reads how the arguments bound
public partial class BindingToolset : Toolset
{
    public BindingToolset()
    {
        AddTool(Refund)
            .WithDescription("Refunds an order.");

        AddTool(Remind)
            .WithDescription("Sets a reminder.");

        AddTool(Ship)
            .WithDescription("Ships a parcel.");

        AddTool(Report)
            .WithDescription("Reports a lookup that failed.");

        AddTool(Wait)
            .WithDescription("Waits for a handle.");
    }

    /// <param name="number">The order's number.</param>
    /// <param name="reason">Why it is refunded.</param>
    public static Refunded Refund(int number, string reason) => new(number, reason);

    /// <param name="days">In how many days, or null for today.</param>
    /// <param name="note">The reminder's text, or null for none.</param>
    public static Reminder Remind(int? days, string? note) => new(days, note);

    /// <param name="parcel">The parcel to ship.</param>
    public static Parcel Ship(Parcel parcel) => parcel;

    // FailedLookup holds an Exception, and a native integer is no JSON value either: neither gets a schema
    /// <param name="lookup">The lookup that failed.</param>
    public static string Report(FailedLookup lookup) => lookup.Message;

    /// <param name="handle">The handle to wait for.</param>
    public static bool Wait(nint handle) => handle != 0;
}
