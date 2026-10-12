using System.Text.Json.Serialization;
using Typeshift.Samples.Docs.Knowledge;
using Typeshift.Samples.Docs.Tickets;

namespace Typeshift.Samples.Docs;

[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(IReadOnlyList<Ticket>))]
[JsonSerializable(typeof(IReadOnlyList<Article>))]
internal partial class SupportJsonContext : JsonSerializerContext;
