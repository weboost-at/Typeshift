using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Scenarios;

// The README's web app: AddTypeshift() and a chat client, with no store, so Typeshift keeps everything in memory
internal static class MinimalApi
{
    public static async Task RunAsync(IChatClient client)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // begin-snippet: minimal-api
        builder.Services.AddTypeshift();
        builder.Services.AddChatClient(client);

        var app = builder.Build();

        app.MapGet("/research/{city}", async (string city, AgentHost<ResearchAgent> research) =>
            (await research.RunAsync(city)).Text);
        // end-snippet

        await using (app)
        {
            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
                .Addresses.First();

            using var http = new HttpClient { BaseAddress = new Uri(address) };

            var answer = await http.GetStringAsync("/research/Vienna");
            if (answer != "Found Danube Robotics, Prater Motion and Belvedere Optics.")
                throw new InvalidOperationException($"Unexpected answer: {answer}");

            await app.StopAsync();
        }
    }
}
