using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Typeshift.Tests.BuiltIn.Support;

// Answers every request with one response, or never, as a server that hangs would
public sealed class ScriptedHttpHandler(HttpResponseMessage? response) : HttpMessageHandler
{
    public static ScriptedHttpHandler Page(string body, string mediaType) =>
        Answer(new StringContent(body, Encoding.UTF8, mediaType));

    public static ScriptedHttpHandler Body(Stream body, string mediaType)
    {
        var content = new StreamContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return Answer(content);
    }

    public static ScriptedHttpHandler Answer(HttpContent content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        };

        return new(response);
    }

    public static ScriptedHttpHandler Status(HttpStatusCode status) => new(new HttpResponseMessage(status));

    public static ScriptedHttpHandler Hanging() => new(response: null);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (response is null) return Stall.Until<HttpResponseMessage>(ct);

        response.RequestMessage = request;
        return Task.FromResult(response);
    }
}
