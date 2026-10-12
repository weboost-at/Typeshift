using Typeshift.Agents;
using Typeshift.Samples.Docs.Agents;
using Typeshift.Samples.Docs.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Docs.Scenarios;

internal static class FileStore
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("typeshift-docs-");

        try
        {
            await using var services = Host.Create(services =>
            {
                // begin-snippet: file-store-registration
                services.Configure<FileStoreOptions>(options => options.Root = root.FullName);
                services.AddTypeshiftStore<FileTypeshiftStore, Guid>();
                // end-snippet
            });

            var support = services.GetRequiredService<AgentRuntime<SupportAgent>>();
            var customer = Guid.NewGuid();

            await support.ConfigureAsync(customer, settings => settings with { Temperature = 0.1f });

            Expect.That((await support.LoadSettingsAsync(customer)).Temperature == 0.1f, "the file store loads back");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
