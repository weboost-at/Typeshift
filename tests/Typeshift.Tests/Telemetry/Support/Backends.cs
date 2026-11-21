using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Telemetry.Support;

// The backends of the clients a test hands out, one for each run, in order
public sealed class Backends(IEnumerable<ChatClientMetadata> backends) : Queue<ChatClientMetadata>(backends);

// A new client for every run, of the test's scripted client, as a provider of per-tenant clients makes
public sealed class ClientPerCall(IChatClient inner) : DelegatingChatClient(inner);
