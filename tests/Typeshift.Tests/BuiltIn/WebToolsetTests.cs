using System.Net;
using System.Net.Sockets;
using Typeshift.Tests.Agents.Support;
using Typeshift.Toolsets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.BuiltIn;

public class WebToolsetTests : AgentTestBase
{
    [Fact]
    public async Task Given_A_Url_That_Is_Not_Http_Fetching_It_Fails()
    {
        // Act
        var fetch = () => WebToolset.WebFetchAsync(new Uri("file:///etc/passwd"), Scope, Ct);

        // Assert
        await fetch.Should().ThrowAsync<ArgumentException>().WithMessage("*http*");
    }

    [Fact]
    public async Task Given_No_Fetcher_Fetching_Fails_Naming_The_Registration()
    {
        // Act
        var fetch = () => WebToolset.WebFetchAsync(new Uri("https://example.com/"), Scope, Ct);

        // Assert
        await fetch.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithWebFetch()*");
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public async Task Given_The_Built_In_Fetcher_A_Loopback_Page_Is_Refused_Before_It_Connects(string host)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTypeshift().WithWebFetch();

        await using var provider = services.BuildServiceProvider();
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();

        var url = new Uri($"http://{host}:{((IPEndPoint)server.LocalEndpoint).Port}/");

        // Act
        var fetch = () => WebToolset.WebFetchAsync(url, provider, Ct);

        // Assert
        await fetch.Should().ThrowAsync<HttpRequestException>().WithMessage("*no public address*");
        server.Pending().Should().BeFalse();
    }

    [Fact]
    public async Task Given_No_Search_Engine_Searching_Fails_Naming_The_Registration()
    {
        // Act
        var search = () => WebToolset.WebSearchAsync(Data.Tool.Query, Scope, ct: Ct);

        // Assert
        await search.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithWebSearch*");
    }
}
