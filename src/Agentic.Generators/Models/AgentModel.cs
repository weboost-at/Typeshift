namespace Agentic.Generators.Models;

internal sealed record AgentModel(
    TypeModel Type,
    bool IsPartial,
    EquatableArray<ChatOptionModel> ConfigurableOptions,
    EquatableArray<ToolsetUseModel> Uses,
    EquatableArray<DiagnosticInfo> Diagnostics);

// A chat option the agent's settings may override; NullableType is its type made nullable
internal sealed record ChatOptionModel(string Name, string NullableType, StorageModel Storage);

// All for UseToolset<T>(), else the names of the selection methods called; IsPublic when other assemblies can name T
internal sealed record ToolsetUseModel(
    string Toolset,
    bool IsPublic,
    bool All,
    EquatableArray<string> Selected,
    LocationInfo? Location);
