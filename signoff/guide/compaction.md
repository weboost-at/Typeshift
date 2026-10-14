# Compaction

A long conversation outgrows the model's context. Compaction shortens what a resumed conversation sends; the stored
conversation keeps everything.

<!-- snippet: compaction -->
<a id='snippet-compaction'></a>
```cs
WithCompaction()
    .WithMaxMessages(40)
    .WithSummaryModel("fast-model");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/SupportAgent.cs#L35-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-compaction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **`WithMaxMessages(40)`** sends at most the 40 latest messages, never splitting a tool call from its result. The
  instructions always go, and so does the latest `write_todos` call.
- **`WithSummaryModel("fast-model")`** summarizes the messages that no longer fit with that model instead of dropping
  them. The summary is stored with the conversation as a session of its own, so later runs start from it instead of
  summarizing again.

Compaction runs when a conversation is resumed, not between the calls of one run's tool loop.
