using System.Text.Json.Serialization;
using Agentic.Samples.Docs.Knowledge;
using Agentic.Samples.Docs.Tickets;

namespace Agentic.Samples.Docs;

[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(IReadOnlyList<Ticket>))]
[JsonSerializable(typeof(IReadOnlyList<Article>))]
internal partial class SupportJsonContext : JsonSerializerContext;
