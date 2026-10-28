using System.Net;
using Typeshift.Tests.BuiltIn.Support;
using Typeshift.Toolsets;
using FluentAssertions;

namespace Typeshift.Tests.BuiltIn;

public class PublicWebFetchTests : UnitTestBase
{
    private static readonly Uri Url = new("https://example.com/refunds");

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.8")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.5.4")]
    [InlineData("192.168.1.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.251")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    public void Given_An_Address_That_Is_Not_Public_It_Is_Refused(string address)
    {
        // Act
        var allowed = PublicWebFetch.IsPublic(IPAddress.Parse(address));

        // Assert
        allowed.Should().BeFalse();
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.215.14")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void Given_A_Public_Address_It_Is_Allowed(string address)
    {
        // Act
        var allowed = PublicWebFetch.IsPublic(IPAddress.Parse(address));

        // Assert
        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Given_A_Page_Its_Text_Leaves_Out_Scripts_Styles_And_Tags()
    {
        // Arrange
        const string html =
            "<html><head><style>p { color: red }</style></head>"
            + "<body><script>steal()</script><p>Refunds take <b>five</b> days.</p><!-- draft --></body></html>";

        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Page(html, "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("Refunds take five days.");
    }

    [Fact]
    public async Task Given_A_Page_Its_Entities_Are_Decoded_And_Each_Block_Is_A_Line()
    {
        // Arrange
        const string html =
            "<h1>Fish &amp; chips</h1><p>Open&nbsp;daily,\n   <i>noon</i> to ten</p><ul><li>Cod</li><li>Hake</li></ul>";

        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Page(html, "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("Fish & chips\nOpen daily, noon to ten\nCod\nHake");
    }

    [Fact]
    public async Task Given_A_Page_With_A_Title_It_Is_The_Pages_Title()
    {
        // Arrange
        const string html = "<html><head><title> Refunds &amp; returns </title></head><body>Hello</body></html>";
        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Page(html, "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Title.Should().Be("Refunds & returns");
    }

    [Fact]
    public async Task Given_Plain_Text_It_Is_Returned_As_It_Is()
    {
        // Arrange
        const string text = "name,days\nrefund,<5>";
        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Page(text, "text/csv"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be(text);
    }

    [Fact]
    public async Task Given_An_Image_Fetching_It_Fails_Naming_Its_Type()
    {
        // Arrange
        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Page("PNG", "image/png"));

        // Act
        var act = () => fetch.FetchAsync(Url, Ct);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*image/png*");
    }

    [Fact]
    public async Task Given_A_Page_That_Is_Not_Found_Fetching_It_Fails_Naming_The_Status()
    {
        // Arrange
        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Status(HttpStatusCode.NotFound));

        // Act
        var act = () => fetch.FetchAsync(Url, Ct);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*404*");
    }

    [Fact]
    public async Task Given_Text_Past_The_Limit_It_Is_Cut_With_A_Note()
    {
        // Arrange
        var text = new string('x', PublicWebFetch.MaxLength + 1);
        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Page(text, "text/plain"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be(new string('x', PublicWebFetch.MaxLength) + "\n[The page goes on past this point.]");
    }

    [Fact]
    public async Task Given_A_Page_Past_The_Byte_Limit_What_Follows_The_Limit_Is_Not_Read()
    {
        // Arrange
        var html = string.Concat(Enumerable.Repeat("<i></i>", PublicWebFetch.MaxBytes / 7 + 1)) + "<p>Hidden</p>";
        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Page(html, "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Server_That_Never_Answers_Fetching_Fails_After_The_Timeout()
    {
        // Arrange
        using var fetch = new PublicWebFetch(Time, ScriptedHttpHandler.Hanging());

        // Act
        var fetching = fetch.FetchAsync(Url, Ct);
        Time.Advance(PublicWebFetch.Timeout);

        var act = () => fetching;

        // Assert
        await act.Should().ThrowAsync<TimeoutException>().WithMessage("*30 seconds*");
    }
}
