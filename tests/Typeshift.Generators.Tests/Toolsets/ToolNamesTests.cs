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

    [Theory]
    [InlineData("da-DK", "EnableMFAAsync", "enable_mfa")]
    [InlineData("nb-NO", "SearchJIRAAsync", "search_jira")]
    [InlineData("fo-FO", "RunQAAsync", "run_qa")]
    public void Given_A_Build_Machine_Whose_Culture_Reads_AA_As_One_Letter_The_Async_Suffix_Is_Still_Dropped(
        string culture,
        string method,
        string tool)
    {
        // Arrange
        using var machine = new BuildCulture(culture);

        // Act
        var name = ToolNames.FromMethod(method);

        // Assert
        name.Should().Be(tool);
    }

    [Theory]
    [InlineData("echo")]
    [InlineData("get_v2")]
    [InlineData("base64_encode")]
    public void Given_Lower_Snake_Case_A_Name_Is_A_Tool_Name(string name)
    {
        // Act
        var accepted = ToolNames.IsLowerSnakeCase(name);

        // Assert
        accepted.Should().BeTrue();
    }

    [Theory]
    [InlineData("Echo")]
    [InlineData("echo_")]
    [InlineData("_echo")]
    [InlineData("2nd_echo")]
    [InlineData("echo__twice")]
    [InlineData("echo\n")]
    [InlineData("")]
    public void Given_Anything_Else_Than_Lower_Snake_Case_A_Name_Is_No_Tool_Name(string name)
    {
        // Act
        var accepted = ToolNames.IsLowerSnakeCase(name);

        // Assert
        accepted.Should().BeFalse();
    }
}
