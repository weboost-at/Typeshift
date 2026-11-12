namespace Typeshift.Tests.Hosting.Support;

// The providers of the vendor agent, an enum, so one app runs agents of two provider types
public enum Vendor
{
    Probe,
    Other
}

// D77: its provider id type is the enum, from WithModel
/// <summary>
/// Answers on a provider of an enum.
/// </summary>
public partial class VendorAgent : Agent
{
    public const string Model = "vendor-model";

    public VendorAgent()
    {
        WithChatOptions()
            .WithModel(Vendor.Probe, Model)
            .Configurable(o => o.ModelId);
    }
}
