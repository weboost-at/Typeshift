using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Typeshift.Tests.BuiltIn.Support;
using Typeshift.Toolsets;
using FluentAssertions;

namespace Typeshift.Tests.BuiltIn;

public class PublicWebFetchTests : UnitTestBase
{
    private static readonly Uri Url = new("https://example.com/refunds");

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.8")]
    [InlineData("100.64.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.5.4")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.251")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::7f00:1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("64:ff9b:1::a00:1")]
    [InlineData("2001::1")]
    [InlineData("2002:7f00:1::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("fe80::1")]
    [InlineData("fec0::1")]
    [InlineData("ff02::1")]
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
    [InlineData("64:ff9b::808:808")]
    public void Given_A_Public_Address_It_Is_Allowed(string address)
    {
        // Act
        var allowed = PublicWebFetch.IsPublic(IPAddress.Parse(address));

        // Assert
        allowed.Should().BeTrue();
    }

    [Fact]
    public void Given_The_Handler_It_Connects_Itself_Without_A_Proxy_Or_Cookies()
    {
        // Act
        using var handler = PublicWebFetch.CreateHandler();

        // Assert
        handler.UseProxy.Should().BeFalse();
        handler.UseCookies.Should().BeFalse();
        handler.ConnectCallback.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_A_Page_Its_Text_Leaves_Out_Scripts_Styles_And_Tags()
    {
        // Arrange
        const string html =
            "<html><head><title>Refunds</title></head><body><style>p { color: red }</style>"
            + "<script>steal()</script><p>Refunds take <b>five</b> days.</p><!-- draft --></body></html>";

        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page(html, "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("Refunds take five days.");
    }

    [Fact]
    public async Task Given_Entities_The_Text_Decodes_Them()
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page("<p>Fish &amp; chips&nbsp;&lt;3</p>", "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("Fish & chips <3");
    }

    [Fact]
    public async Task Given_Blocks_Each_Is_A_Line_Of_Its_Own()
    {
        // Arrange
        const string html = "<h1>Menu</h1><p>Open daily,\n   <i>noon</i> to ten</p><ul><li>Cod</li><li>Hake</li></ul>";

        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page(html, "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("Menu\nOpen daily, noon to ten\nCod\nHake");
    }

    [Fact]
    public async Task Given_A_Table_Its_Cells_Are_Kept_Apart()
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page("<table><tr><td>Cod</td><td>£5</td></tr></table>", "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("Cod £5");
    }

    [Fact]
    public async Task Given_A_Pre_Block_Its_Lines_Are_Kept()
    {
        // Arrange
        const string html = "<p>Run:</p><pre><code>dotnet build\n  --no-restore</code></pre><p>Done.</p>";

        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page(html, "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("Run:\ndotnet build\n  --no-restore\nDone.");
    }

    [Fact]
    public async Task Given_A_Less_Than_Sign_That_Opens_No_Tag_The_Text_After_It_Stays()
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page("<p>3 < 5, and 5 is more</p>", "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("3 < 5, and 5 is more");
    }

    [Fact]
    public async Task Given_A_Page_With_A_Title_It_Is_The_Pages_Title()
    {
        // Arrange
        const string html = "<html><head><title> Refunds &amp; returns </title></head><body>Hello</body></html>";

        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page(html, "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Title.Should().Be("Refunds & returns");
    }

    [Fact]
    public async Task Given_A_Legacy_Charset_The_Page_Is_Read_In_It()
    {
        // Arrange
        var content = new ByteArrayContent([0x80, 0x35]);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; charset=windows-1252");

        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Answer(content));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("€5");
    }

    [Fact]
    public async Task Given_A_Byte_Order_Mark_It_Stays_Out_Of_The_Text()
    {
        // Arrange
        var content = new ByteArrayContent([.. Encoding.UTF8.Preamble, .. "<p>Refunds</p>"u8]);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Answer(content));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be("Refunds");
    }

    [Fact]
    public async Task Given_Plain_Text_It_Is_Returned_As_It_Is()
    {
        // Arrange
        const string text = "name,days\nrefund,<5>";

        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page(text, "text/csv"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be(text);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task Given_A_Type_That_Is_Not_Text_Fetching_It_Fails_Naming_The_Type(string mediaType)
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page("PK", mediaType));

        // Act
        var act = () => fetch.FetchAsync(Url, Ct);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{mediaType}*");
    }

    [Fact]
    public async Task Given_No_Content_Type_Fetching_Fails()
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Answer(new ByteArrayContent("Refunds"u8.ToArray())));

        // Act
        var act = () => fetch.FetchAsync(Url, Ct);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no named type*");
    }

    [Fact]
    public async Task Given_A_Page_That_Is_Not_Found_Fetching_It_Fails_Naming_The_Status()
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Status(HttpStatusCode.NotFound));

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

        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Page(text, "text/plain"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().Be(new string('x', PublicWebFetch.MaxLength) + "\n[The page goes on past this point.]");
    }

    [Fact]
    public async Task Given_A_Body_That_Never_Ends_Fetching_Stops_At_The_Byte_Limit()
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Body(ScriptedBody.Endless("<i></i>"), "text/html"));

        // Act
        var page = await fetch.FetchAsync(Url, Ct);

        // Assert
        page.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_A_Server_That_Never_Answers_Fetching_Fails_After_The_Time_Limit()
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Hanging());

        // Act
        var fetching = fetch.FetchAsync(Url, Ct);
        Time.Advance(PublicWebFetch.TimeLimit);

        var act = () => fetching;

        // Assert
        await act.Should().ThrowAsync<TimeoutException>().WithMessage("*30 seconds*");
    }

    [Fact]
    public async Task Given_A_Body_That_Stalls_Fetching_Fails_After_The_Time_Limit()
    {
        // Arrange
        using var fetch = new PublicWebFetch(
            time: Time,
            handler: ScriptedHttpHandler.Body(ScriptedBody.Stalled(), "text/html"));

        // Act
        var fetching = fetch.FetchAsync(Url, Ct);
        Time.Advance(PublicWebFetch.TimeLimit);

        var act = () => fetching;

        // Assert
        await act.Should().ThrowAsync<TimeoutException>().WithMessage("*30 seconds*");
    }
}
