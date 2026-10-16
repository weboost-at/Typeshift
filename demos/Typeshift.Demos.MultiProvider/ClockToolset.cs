using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Demos.MultiProvider;

internal record ClockSettings
{
    public string Format { get; init; } = "HH:mm";

    public bool IncludeDate { get; init; }
}

internal partial class ClockToolset : Toolset
{
    public ClockToolset()
    {
        WithDisplayName("Clock");

        AddTool(GetTime)
            .ReadOnly()
            .WithSettings<ClockSettings>()
            .Configurable(s => s.Format);
    }

    /// <summary>
    /// Tells the current time in a time zone.
    /// </summary>
    /// <param name="timeZone">An IANA time zone id, such as Europe/Vienna.</param>
    public static string GetTime(string timeZone, ClockSettings settings, IServiceProvider services)
    {
        var time = services.GetService<TimeProvider>() ?? TimeProvider.System;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var now = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone);

        return now.ToString(settings.IncludeDate ? $"yyyy-MM-dd {settings.Format}" : settings.Format);
    }
}
