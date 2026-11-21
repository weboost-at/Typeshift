using Typeshift.Tests.Hosting.Support;

namespace Typeshift.Tests.MultiAgent.Support;

// D87: agents a run reaches whose providers the tests leave unserved, unless a call site or the settings name another

/// <summary>
/// Answers on a provider of an enum the multi-agent tests register no client for.
/// </summary>
public partial class UnservedHelperAgent : Agent
{
    public const string Model = "unserved-model";

    public UnservedHelperAgent() => WithChatOptions().WithProvider(Vendor.Probe).WithModel(Model);
}

/// <summary>
/// Answers on a provider of an enum nobody serves, unless its settings name another.
/// </summary>
public partial class SwitchableHelperAgent : Agent
{
    public const string Model = "switchable-model";

    public SwitchableHelperAgent()
    {
        WithChatOptions()
            .WithProvider(Vendor.Probe)
            .WithModel(Model)
            .Configurable(o => o.ModelId);
    }
}

public partial class UnservedLeadAgent : Agent
{
    public UnservedLeadAgent()
    {
        WithChatOptions().WithProvider("scripted").WithModel("lead-model");

        UseAgent<UnservedHelperAgent>();
    }
}

public partial class SwitchableLeadAgent : Agent
{
    public SwitchableLeadAgent()
    {
        WithChatOptions().WithProvider("scripted").WithModel("lead-model");

        UseAgent<SwitchableHelperAgent>();
    }
}

// Hands off to an agent nothing serves
public partial class UnservedFrontAgent : Agent
{
    public UnservedFrontAgent()
    {
        WithDescription("Finds out what the customer needs.");

        WithChatOptions().WithProvider("scripted").WithModel("front-model");

        UseHandoff<UnservedBackAgent>();
    }
}

/// <summary>
/// Settles billing on a provider nobody serves.
/// </summary>
public partial class UnservedBackAgent : Agent
{
    public UnservedBackAgent() => WithChatOptions().WithProvider(Vendor.Probe).WithModel("unserved-back-model");
}
