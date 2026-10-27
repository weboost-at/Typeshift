using Microsoft.Extensions.AI;

namespace Typeshift;

// D32: a model call's messages and what its provider reported for it, which calibrates compaction's estimate
internal sealed record ReportedCall(IReadOnlyList<ChatMessage> Call, UsageDetails Usage);
