using Typeshift.Json;
using Typeshift.Persistence;
using Typeshift.Settings;
using Typeshift.Toolsets;
using Microsoft.Extensions.AI;

namespace Typeshift.Tools;

internal sealed class OverridableToolBinding<TSettings, TOverrides>(
    Func<ToolDefinition, TSettings, AgentJson, AIFunction> createFunction)
    : SettingsToolBinding<TSettings>(createFunction)
    where TSettings : class, new()
    where TOverrides : class, IToolOverrides<TSettings>, IStorable<TOverrides>, new()
{
    internal override async Task<IToolOverrides?> LoadDefaultsAsync<TId>(
        ITypeshiftStore<TId> store,
        TId id,
        CancellationToken ct) =>
        await store.LoadAsync<TOverrides>(id, ct);
}
