using Typeshift.Generators.Parsing;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Toolsets;

public class ToolNamesTests
{
    [Theory]
    [InlineData("SearchTickets", "search_tickets")]
    [InlineData("CloseTicketAsync", "close_ticket")]
    [InlineData("ParseHTMLPage", "parse_html_page")]
    [InlineData("GetV2", "get_v2")]
    [InlineData("Base64Encode", "base64_encode")]
    [InlineData("IOStream", "io_stream")]
    [InlineData("Read_File", "read_file")]
    [InlineData("Async", "async")]
    [InlineData("_Echo", "echo")]
    public void A_Method_Name_Becomes_Lower_Snake_Case(string method, string tool)
    {
        // Act
        var name = ToolNames.FromMethod(method);

        // Assert
        name.Should().Be(tool);
    }
}
