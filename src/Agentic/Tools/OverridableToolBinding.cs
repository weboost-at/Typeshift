using Agentic.Json;
using Agentic.Persistence;
using Agentic.Settings;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Tools;

internal sealed class OverridableToolBinding<TSettings, TOverrides>(
    Func<ToolDefinition, TSettings, AgentJson, AIFunction> createFunction)
    : SettingsToolBinding<TSettings>(createFunction)
    where TSettings : class, new()
    where TOverrides : class, IToolOverrides<TSettings>, IStorable<TOverrides>, new()
{
    internal override async Task<IToolOverrides?> LoadDefaultsAsync<TId>(
        IAgenticStore<TId> store,
        TId id,
        CancellationToken ct) =>
        await store.LoadAsync<TOverrides>(id, ct);
}
