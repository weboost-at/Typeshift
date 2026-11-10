using System.Text.Json;
using Microsoft.Extensions.AI;
using Typeshift.Generated;

namespace Typeshift;

// D65: the format a sub-agent answers in, and its answer as the JSON its caller's model reads
internal abstract class SubAgentResult
{
    public abstract ChatResponseFormat GetFormat(ToolJson json);

    public abstract JsonElement Read(ChatResponse response, ToolJson json, string agent);
}

internal sealed class SubAgentResult<TResult> : SubAgentResult
{
    public override ChatResponseFormat GetFormat(ToolJson json) => json.GetResultFormat<TResult>().Format;

    public override JsonElement Read(ChatResponse response, ToolJson json, string agent)
    {
        var typed = new ChatResponse<TResult>(response, json.GetResultFormat<TResult>().SerializerOptions);

        return typed.TryGetResult(out var result)
            ? JsonSerializer.SerializeToElement(result, json.GetTypeInfo<TResult>())
            : throw new InvalidOperationException(
                $"{agent} did not answer in the JSON schema it was given. It answered: {response.Text}");
    }
}
