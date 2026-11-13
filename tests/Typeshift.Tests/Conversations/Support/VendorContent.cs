using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Conversations.Support;

// A content type of a provider adapter's own, which Microsoft.Extensions.AI's content contract does not know
public sealed class VendorContent : AIContent;
