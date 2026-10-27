---
title: Built-in toolsets
description: The to-do, file, shell and web toolsets that ship with Typeshift, what each needs from your app, and where they are not safe.
---

# Built-in toolsets

Typeshift ships four toolsets for a coding agent: to-dos, files, the shell and the web. An agent offers and configures
them like your own.

## Offering them

The built-in toolsets and the interfaces they need are in the `Typeshift.Toolsets` namespace, so add
`using Typeshift.Toolsets;` to the file. An agent then offers them with `UseToolset<T>()`:

<!-- snippet: coding-agent -->
<a id='snippet-coding-agent'></a>
```cs
internal partial class CodingAgent : Agent
{
    public CodingAgent()
    {
        WithChatOptions()
            .WithModel("anthropic", "claude-sonnet-5-5");

        UseToolset<TodoToolset>();
        UseToolset<FileSystemToolset>();
        UseToolset<ShellToolset>();
        UseToolset<WebToolset>()
            .Include(t => t.WebFetch);

        UseAgent<ExploreAgent>()
            .InBackground();

        WithRunOptions()
            .WithMode(AgentMode.Plan)
            .WithMaxToolIterations(500)
            .WithMaxInputTokens(150_000)
            .WithSummaryModel("claude-haiku-5-5")
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations)
            .Configurable(r => r.SummaryModel);

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L5-L46' title='Snippet source file'>snippet source</a> | <a href='#snippet-coding-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This agent offers all four toolsets, but only `web_fetch` of the web tools, and `ConfigureRunCommand()` sets the
shell's settings for it. Its run options start runs in [plan mode](./tool-approval.md#plan-mode), allow up to 500
rounds of tool calls and compact the history at 150,000 input tokens, which makes it a
[long-running agent](./conversations.md#compaction). `InBackground()` lets its `ExploreAgent` sub-agent work
[in the background](./multi-agent.md#in-the-background).

## What each needs

| Toolset | Tools | Needs |
|---|---|---|
| `TodoToolset` | `write_todos` | nothing |
| `FileSystemToolset` | `read_file`, `list_files`, `search_files`, `write_file`, `edit_file`, `delete_file` | `WithFileSystemRoot<T>()` |
| `ShellToolset` | `run_command` | `WithFileSystemRoot<T>()` |
| `WebToolset` | `web_search`, `web_fetch` | `WithWebSearch<T>()`, `WithWebFetch<T>()` |

Register these services with the `With...` calls on the chain that `AddTypeshift()` returns. A tool whose service is
missing fails its call with a message that names the registration. `WithApprovalRule<ShellGuard>()` adds an
[approval rule](./tool-approval.md#approval-rules) that rejects the shell commands you never allow:

<!-- snippet: built-in-registration -->
<a id='snippet-built-in-registration'></a>
```cs
services.AddSingleton<HttpClient>();

services.AddTypeshift()
    .WithFileSystemRoot<CustomerWorkspace>()
    .WithWebSearch<SearchApi>()
    .WithWebFetch<HttpWebFetch>()
    .WithApprovalRule<ShellGuard>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L87-L95' title='Snippet source file'>snippet source</a> | <a href='#snippet-built-in-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The sample's `HttpWebFetch` reads pages with a plain `HttpClient` and checks no address, so it is not safe as it
stands: your fetcher must refuse private addresses, as the warning under [Web](#web) explains.

## Todos

`write_todos` replaces the model's to-do list, a list of titles, each `Pending`, `InProgress` or `Completed`. The
list is kept only in the conversation: the latest `write_todos` call carries the current list, and
[compaction](./conversations.md#compaction) never drops that call. Plan mode offers it.

## Files

The file tools work within the root an `IFileSystemRoot` gives. Typeshift resolves it from the run's services, so
each customer can have a folder of their own:

<!-- snippet: file-system-root -->
<a id='snippet-file-system-root'></a>
```cs
internal sealed class CustomerWorkspace(ICurrentCustomer customer) : IFileSystemRoot
{
    public string Path => System.IO.Path.Combine("/srv/workspaces", customer.Id.ToString());
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/CustomerWorkspace.cs#L5-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-file-system-root' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Paths are relative to the root. The tools refuse every absolute path, every path with `..` and every path through a
link that leads outside the root, and tell the model why.

| Tool | Does | Marked |
|---|---|---|
| `read_file` | Reads a text file, numbering its lines. | `ReadOnly()` |
| `list_files` | Lists the files and folders of a folder. | `ReadOnly()` |
| `search_files` | Finds the lines that match a regular expression, in every file under a folder. | `ReadOnly()` |
| `write_file` | Writes a text file, replacing it if it exists and creating its folders. | |
| `edit_file` | Replaces one exact occurrence of a text in a file. | |
| `delete_file` | Deletes a file. | `RequiresApproval()` |

Plan mode offers the three `ReadOnly()` tools, and an agent that should only read
[includes](./defining-agents.md#selecting-tools) only those. `FileSystemSettings` caps what one call returns:
2,000 lines, matches or entries, 2,000 characters per line and 65,536 characters in all. An agent can change the caps
with `ConfigureReadFile()`, `ConfigureListFiles()` and `ConfigureSearchFiles()`, as `ConfigureRunCommand()` does for
the shell. Stored settings cannot, because none of the record's members is configurable. A search pattern is a .NET
regular expression, matched in linear time where the pattern allows it.

::: danger The root is not a sandbox
A link swapped in while a tool runs still leads outside the root. Give each customer a container when the workspace
holds what others must not reach.
:::

## Shell

`run_command` runs a command with the root as its working folder, and every call needs approval. Your
[approval rules](./tool-approval.md#approval-rules) see each call before the run asks your app, so a rule can reject
the commands you never allow. A command still running at the timeout is killed, with the processes it started. A
process the command leaves running in the background outlives the call, and because it holds the output open, the
call returns a second past the timeout.

`ShellSettings` sets the shell (`/bin/sh`), the timeout (30 seconds) and how much of each stream the model receives:
65,536 characters, from its start and its end.

::: danger The working folder is not a sandbox
The root is the working folder of each command, nothing more. Run your app in a container when commands must not
reach further.
:::

::: danger Approving commands by rule is rarely safe
A rule that approves harmless-looking commands also approves whatever code those commands run. Because `write_file`
runs without asking, the model can first write the configuration or the code that `git status` or `dotnet test` then
runs.
:::

## Web

`web_search` and `web_fetch` call the services your app registers. Typeshift ships no search or fetch provider, so you
implement `IWebSearch` and `IWebFetch` over the service you choose, such as Tavily, Firecrawl, Brave or a plain
`HttpClient`. This search calls a JSON API:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/SearchApi.cs#L7-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-web-search' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Both tools are `ReadOnly()`, so plan mode offers them. `web_fetch` takes absolute http and https URLs only.

::: danger Your fetcher must refuse private addresses
The model picks the URLs `web_fetch` reads. A fetcher on your app's network must refuse loopback, private and
link-local addresses where it connects (`SocketsHttpHandler.ConnectCallback`), or the model reads the cloud's
metadata service. Check the address there, not the host name, which a DNS answer can change.
:::

::: danger What the web returns is untrusted
Search results and fetched pages are untrusted text, never instructions. Anyone can write a page the model reads.
:::

## Related

- [Toolsets](./toolsets.md): declaring your own toolsets the same way.
- [Agents](./defining-agents.md#selecting-tools): picking a toolset's tools with `Include` and `Exclude`.
- [Approval rules](./tool-approval.md#approval-rules): the rules that guard the shell.
- [Compaction](./conversations.md#compaction): the coding agent's run options and compaction.
- [Generated members](./generated-members.md): `ConfigureRunCommand()` and the other `Configure{Tool}()` methods.
