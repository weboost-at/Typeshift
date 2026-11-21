using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Typeshift;

// D89: the kinds a model policy's rules handle. Typeshift tells those of .NET's own exceptions, and your app's
// classification those of a client library, which Typeshift does not reference and reads no member of by reflection
/// <summary>
/// The kind of failure a model call met, which the rules of a <see cref="ModelPolicy"/> handle. The kinds are flags,
/// so a rule handles several, as <see cref="Transient"/> does. Typeshift tells the kind of .NET's own exceptions,
/// such as an <see cref="HttpRequestException"/> by its status, and
/// <see cref="TypeshiftOptions.ClassifyModelFailure"/> the kind of a client library's own.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/models#which-failure-is-which">Which failure is
/// which</see>.
/// </summary>
[Flags]
[Experimental(ExperimentalIds.ModelPolicy, UrlFormat = ExperimentalIds.ModelPolicyUrl)]
public enum ModelFailure
{
    /// <summary>
    /// No failure a rule handles: the exception reaches your app as the client threw it.
    /// </summary>
    None = 0,

    /// <summary>
    /// The provider refused the call for its rate or its quota, with status 429.
    /// </summary>
    RateLimit = 1,

    /// <summary>
    /// No answer in time: the policy's timeout, a <see cref="TimeoutException"/>, a cancellation your app did not ask
    /// for, or status 408 or 504.
    /// </summary>
    Timeout = 2,

    /// <summary>
    /// The provider could not be reached or failed itself: a status of 500 or more but 504, a request that got no
    /// status, or a failure of the socket or the stream.
    /// </summary>
    Unavailable = 4,

    /// <summary>
    /// The provider refused the key, with status 401 or 403.
    /// </summary>
    Auth = 8,

    /// <summary>
    /// The provider's content filter refused the input. Only your app's classification tells this kind.
    /// </summary>
    ContentFilter = 16,

    /// <summary>
    /// The provider refused the request itself, such as a model it does not know or an input too long, with status
    /// 400, 404, 413 or 422.
    /// </summary>
    Rejected = 32,

    /// <summary>
    /// Any other exception, until your app's classification tells its kind.
    /// </summary>
    Unknown = 64,

    /// <summary>
    /// The failures that pass on their own: <see cref="RateLimit"/>, <see cref="Timeout"/> and
    /// <see cref="Unavailable"/>.
    /// </summary>
    Transient = RateLimit | Timeout | Unavailable,

    /// <summary>
    /// Every kind.
    /// </summary>
    Any = Transient | Auth | ContentFilter | Rejected | Unknown
}

/// <summary>
/// The members Typeshift adds to its own types.
/// </summary>
public static class TypeshiftExtensions
{
    extension(ModelFailure)
    {
        // D89: the table of statuses Typeshift reads from an HttpRequestException, for your app's classification of a
        // client library's exception that holds a status
        /// <summary>
        /// The kind of failure an HTTP status tells: 429 <see cref="ModelFailure.RateLimit"/>, 408 and 504
        /// <see cref="ModelFailure.Timeout"/>, 401 and 403 <see cref="ModelFailure.Auth"/>, 400, 404, 413 and 422
        /// <see cref="ModelFailure.Rejected"/>, any other of 500 or more <see cref="ModelFailure.Unavailable"/>, and
        /// any other status <see cref="ModelFailure.Unknown"/>. Use it in
        /// <see cref="TypeshiftOptions.ClassifyModelFailure"/> for an exception that holds the status, as
        /// <c>ModelFailure.FromStatus(failed.Status)</c> for the OpenAI client's <c>ClientResultException</c>.
        /// </summary>
        /// <param name="status">The HTTP status the provider answered with.</param>
        /// <returns>The kind.</returns>
        [Experimental(ExperimentalIds.ModelPolicy, UrlFormat = ExperimentalIds.ModelPolicyUrl)]
        public static ModelFailure FromStatus(int status) =>
            status switch
            {
                429 => ModelFailure.RateLimit,
                408 or 504 => ModelFailure.Timeout,
                401 or 403 => ModelFailure.Auth,
                400 or 404 or 413 or 422 => ModelFailure.Rejected,
                >= 500 => ModelFailure.Unavailable,
                _ => ModelFailure.Unknown
            };
    }
}

// D89: the kind of a model call's failure, once it is not the caller's own cancellation or the policy's timeout,
// which the policy's client tells apart itself. Your app's classification answers first, since only it knows a
// client library's exceptions
internal static class ModelFailures
{
    public static ModelFailure Classify(Exception exception, Func<Exception, ModelFailure?>? classify) =>
        classify?.Invoke(exception) ?? exception switch
        {
            OperationCanceledException or TimeoutException => ModelFailure.Timeout,
            HttpRequestException { StatusCode: { } status } => ModelFailure.FromStatus((int)status),
            HttpRequestException or SocketException or IOException => ModelFailure.Unavailable,
            _ => ModelFailure.Unknown
        };
}

// D89: the id of every member of the model policy, which ships experimental, and the section of the guide its
// diagnostic links
internal static class ExperimentalIds
{
    public const string ModelPolicy = "TYPESHIFT001";

    public const string ModelPolicyUrl = "https://weboost-at.github.io/Typeshift/guide/models#the-model-policy";
}
