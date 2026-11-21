namespace Typeshift.Tests.Hosting.Support;

// What a vendor's client library throws, of a type Typeshift does not know, as the OpenAI client's
// ClientResultException is
public sealed class VendorException() : Exception("The vendor's client failed.");
