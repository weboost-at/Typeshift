using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Telemetry.Support;

// The test's scripted client under a backend's metadata, as a provider's client library names its service
public sealed class BackendClient(IChatClient inner, ChatClientMetadata backend) : DelegatingChatClient(inner)
{
    public override object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? backend : base.GetService(serviceType, serviceKey);
}
