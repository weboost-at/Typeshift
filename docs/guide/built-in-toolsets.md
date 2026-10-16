---
title: Built-in toolsets
description: The to-do, file, shell and web toolsets that ship with Typeshift, what each needs from the host, and where they are not safe.
---

# Built-in toolsets

Typeshift ships four toolsets for a coding agent: to-dos, files, the shell and the web. An agent offers and configures
them like your own.

## Offering them

An agent offers the built-in toolsets with `UseToolset<T>()`, like any other:

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

        UseAgent<ExploreAgent>().InBackground();

        WithRunOptions()
            .WithMode(AgentMode.Plan)
            .WithMaxToolIterations(500)
            .WithMaxInputTokens(150_000)
            .WithSummaryModel("fast-model")
            .Configurable(r => r.Mode)
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L5-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-coding-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This agent offers all four toolsets, but only `web_fetch` of the web tools, and `ConfigureRunCommand()` sets the
shell's settings for it. Its run options make it a [long-running agent](./long-running-agents.md#compaction).

## What each needs

| Toolset | Tools | Needs |
|---|---|---|
| `TodoToolset` | `write_todos` | nothing |
| `FileSystemToolset` | `read_file`, `list_files`, `search_files`, `write_file`, `edit_file`, `delete_file` | `WithFileSystemRoot<T>()` |
| `ShellToolset` | `run_command` | `WithFileSystemRoot<T>()` |
| `WebToolset` | `web_search`, `web_fetch` | `WithWebSearch<T>()`, `WithWebFetch<T>()` |

Register the services after `AddTypeshift()`. A tool whose service is missing fails its call with a message that
names the registration:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L86-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-built-in-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Todos

`write_todos` replaces the model's to-do list, a list of titles, each `Pending`, `InProgress` or `Completed`. The
list lives in the conversation: the latest call holds it, and
[compaction](./long-running-agents.md#compaction) never drops that call. Plan mode offers it.

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

Paths are relative to the root. Absolute paths, `..` and links that lead outside are refused, and the model reads
why.

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
2,000 lines, matches or entries, 2,000 characters per line and 65,536 characters in all. A search pattern is a .NET
regular expression, matched in linear time where the pattern allows it.

::: danger The root is not a sandbox
A link swapped in while a tool runs still leads outside the root. Give each customer a container when the workspace
holds what others must not reach.
:::

## Shell

`run_command` runs a command in the root and needs approval for every call, so an
[approval rule](./tool-approval.md#approval-rules) can reject the commands you never allow. A process the command
leaves running in the background outlives the call, which returns at the timeout.

`ShellSettings` set the shell (`/bin/sh`), the timeout (30 seconds) and how much of each stream the model receives:
65,536 characters, from its start and its end.

::: danger The working folder is not a sandbox
The root is the working folder of each command, nothing more. Run the host in a container when commands must not
reach further.
:::

::: danger Approving commands unasked is rarely safe
A rule that approves the commands that look harmless approves the model's code too. While `write_file` runs unasked,
the model can write the configuration or the code that `git status` or `dotnet test` then runs.
:::

## Web

`web_search` and `web_fetch` call the services the host registers. No provider ships with Typeshift, so Tavily,
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/SearchApi.cs#L7-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-web-search' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Both tools are `ReadOnly()`, so plan mode offers them. `web_fetch` takes absolute http and https URLs only.

::: danger Your fetcher must refuse private addresses
The model picks the URLs `web_fetch` reads. A fetcher on the host's network must refuse loopback, private and
link-local addresses where it connects (`SocketsHttpHandler.ConnectCallback`), or the model reads the cloud's
metadata service. Check the address there, not the host name, which a DNS answer can change.
:::

::: danger What the web returns is untrusted
Search results and fetched pages are untrusted text, never instructions. Anyone can write a page the model reads.
:::

## Related

- [Toolsets](./toolsets.md): declaring your own toolsets the same way.
- [Agents](./defining-agents.md#selecting-tools): picking a toolset's tools with `Include` and `Exclude`.
- [Tool approval](./tool-approval.md#approval-rules): the rules that guard the shell.
- [Compaction](./long-running-agents.md#compaction): the coding agent's run options and compaction.
- [Generated members](./generated-members.md): `ConfigureRunCommand()` and the other `Configure{Tool}()` methods.
