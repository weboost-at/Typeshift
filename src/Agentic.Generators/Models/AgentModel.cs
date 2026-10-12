namespace Agentic.Generators.Models;

internal sealed record AgentModel(
    TypeModel Type,
    bool IsPartial,
    EquatableArray<ChatOptionModel> ConfigurableOptions,
    EquatableArray<ToolsetUseModel> Uses,
    EquatableArray<DiagnosticInfo> Diagnostics);

// A chat option the agent's settings may override; NullableType is its type made nullable
internal sealed record ChatOptionModel(string Name, string NullableType, StorageModel Storage);

// Selected holds the selection methods chained after UseToolset<T>(), none for every tool; IsPublic when other
// assemblies can name T
internal sealed record ToolsetUseModel(
    string Toolset,
    bool IsPublic,
    EquatableArray<string> Selected,
    LocationInfo? Location);
