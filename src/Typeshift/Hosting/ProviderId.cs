using System.Globalization;

namespace Typeshift;

// D86: the provider a run names, made where its type is known: a key with its type, in WithProvider and in the
// generated code that applies a settings layer, or a document the call site hands the run (D88). It is equal only to a
// provider of the same type and value, a document to an equal document
internal abstract record ProviderId
{
    // The key a client is registered under, or the document
    public abstract object Value { get; }

    // The provider as a span, an entry, a log line and a message name it, the same whatever the culture
    public abstract string Text { get; }

    public sealed override string ToString() => Text;
}

internal sealed record ProviderId<TProviderId>(TProviderId Id) : ProviderId where TProviderId : notnull
{
    public override object Value => Id;

    public override string Text => Convert.ToString(Id, CultureInfo.InvariantCulture) ?? "";
}

// D88: named by its name or its kind, so its key reaches no span, log line or message
internal sealed record ProviderDocument(ChatProvider Provider) : ProviderId
{
    public override object Value => Provider;

    public override string Text => Provider.Name ?? Provider.Kind;
}
