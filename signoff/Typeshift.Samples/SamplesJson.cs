using System.Text.Json.Serialization;
using Typeshift.Samples.Billing;
using Typeshift.Samples.Knowledge;
using Typeshift.Samples.Quickstart;
using Typeshift.Samples.Scenarios;
using Typeshift.Samples.Tickets;

namespace Typeshift.Samples;

// begin-snippet: json-context
[JsonSerializable(typeof(IReadOnlyList<Company>))]
[JsonSerializable(typeof(IReadOnlyList<Ticket>))]
[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(IReadOnlyList<Article>))]
[JsonSerializable(typeof(IReadOnlyList<Invoice>))]
[JsonSerializable(typeof(RefundStatus))]
internal partial class SamplesJson : JsonSerializerContext;
// end-snippet
