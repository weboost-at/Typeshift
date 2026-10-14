using Typeshift.Tests.Agents.Support;
using Typeshift.Toolsets;
using FluentAssertions;

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
    public async Task Given_No_Search_Engine_Searching_Fails_Naming_The_Registration()
    {
        // Act
        var search = () => WebToolset.WebSearchAsync(Data.Tool.Query, Scope, ct: Ct);

        // Assert
        await search.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WithWebSearch*");
    }
}
