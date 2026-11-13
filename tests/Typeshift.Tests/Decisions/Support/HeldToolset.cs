using Typeshift.Tests.Approval.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Decisions.Support;

// What the held tool waits on: a test sees the call running, then lets it finish or stops its run
public sealed class ToolHold
{
    public TaskCompletionSource Running { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

// A tool behind approval that blocks until released, so a run can be stopped while its approved call runs
public partial class HeldToolset : Toolset
{
    public HeldToolset()
    {
        AddTool(SendRefundAsync)
            .RequiresApproval();
    }

    /// <summary>
    /// Sends a refund.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    public static async Task<string> SendRefundAsync(int number, IServiceProvider services, CancellationToken ct)
    {
        var hold = services.GetRequiredService<ToolHold>();

        services.GetRequiredService<ToolCalls>().Add($"send_refund {number}");
        hold.Running.TrySetResult();
        await hold.Release.Task.WaitAsync(ct);

        return $"refunded {number}";
    }
}

public partial class HeldAgent : Agent
{
    public HeldAgent()
    {
        WithChatOptions().WithModel("scripted", "held-model");

        UseToolset<HeldToolset>();
        UseToolset<GuardedToolset>();
    }
}
