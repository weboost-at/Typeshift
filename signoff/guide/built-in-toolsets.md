# Built-in toolsets

Typeshift ships four toolsets in `Typeshift.Toolsets`, declared like your own, so agents pick and configure their
tools the same way:

<!-- snippet: coding-agent -->
<a id='snippet-coding-agent'></a>
```cs
internal partial class CodingAgent : Agent
{
    public CodingAgent()
    {
        WithChatOptions().WithModel("reasoning-model");

        UseToolset<TodoToolset>();
        UseToolset<FileSystemToolset>();
        UseToolset<ShellToolset>();
        UseToolset<WebToolset>().Include(t => t.WebFetch);

        WithRunOptions()
            .WithMaxToolIterations(500)
            .WithMaxInputTokens(150_000)
            .WithSummaryModel("fast-model")
            .Configurable(r => r.MaxToolIterations);

        ConfigureRunCommand()
            .WithShell("/bin/bash")
            .WithTimeout(TimeSpan.FromMinutes(2));
    }

    protected override string GetInstructions() =>
        """
        Work in the customer's workspace. Plan with write_todos, read before you edit, and run the tests after
        every change.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/CodingAgent.cs#L5-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-coding-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Toolset | Tools | Needs |
|---|---|---|
| `TodoToolset` | `write_todos` | nothing |
| `FileSystemToolset` | `read_file`, `list_files`, `search_files`, `write_file`, `edit_file`, `delete_file` | `WithFileSystemRoot<T>()` |
| `ShellToolset` | `run_command` | `WithFileSystemRoot<T>()` |
| `WebToolset` | `web_search`, `web_fetch` | `WithWebSearch<T>()`, `WithWebFetch<T>()` |

A tool whose service is missing fails its call with a message naming the registration.

<!-- snippet: built-in-registration -->
<a id='snippet-built-in-registration'></a>
```cs
services.AddSingleton<HttpClient>();

services.AddTypeshift()
    .WithFileSystemRoot<CustomerWorkspace>()
    .WithWebSearch<SearchApi>()
    .WithWebFetch<HttpWebFetch>()
    .WithApprovalRule<SafeCommands>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Registration.cs#L41-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-built-in-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Todos

`write_todos` replaces the model's todo list, a list of titles with a status each. The list lives in the conversation:
the latest call holds it, and [compaction](./long-running-agents.md#compaction) never drops that call.

## Files

The file tools work within the root an `IFileSystemRoot` gives. It is resolved from the run's services, so each
customer can have a folder of their own:

<!-- snippet: file-system-root -->
<a id='snippet-file-system-root'></a>
```cs
internal sealed class CustomerWorkspace(ICurrentCustomer customer) : IFileSystemRoot
{
    public string Path => System.IO.Path.Combine("/srv/workspaces", customer.Id.ToString());
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Hosting/CustomerWorkspace.cs#L5-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-file-system-root' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Paths are relative to the root; absolute paths, `..` and links that lead outside are refused. `delete_file` needs
approval. For read-only access, include only `read_file`, `list_files` and `search_files`. `FileSystemSettings`
limits how many lines a read or a search returns.

## Shell

`run_command` runs a command in the root and needs approval for every call. `ShellSettings` set the shell, the
timeout and how much output the model receives (its start and its end). The root is the working folder, not a
sandbox: run the host in a container when commands must not reach further.

## Web

`web_search` and `web_fetch` call the services the host registers; no provider ships with Typeshift, so Tavily,
Firecrawl, Brave or a plain `HttpClient` plug in:

<!-- snippet: web-search -->
<a id='snippet-web-search'></a>
```cs
internal sealed class SearchApi(HttpClient http) : IWebSearch
{
    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int count, CancellationToken ct)
    {
        var url = $"https://search.example.com/?q={Uri.EscapeDataString(query)}&count={count}";
        var hits = await http.GetFromJsonAsync(url, SearchApiJson.Default.SearchHitArray, ct) ?? [];

        return [.. hits.Select(hit => new WebSearchResult(hit.Title, hit.Url, hit.Snippet))];
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Hosting/SearchApi.cs#L7-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-web-search' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Search results and fetched pages are untrusted text, never instructions.
