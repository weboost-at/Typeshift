using Typeshift.Tests.Hosting.Support;
using FluentAssertions;

namespace Typeshift.Tests.Hosting;

// D88: a provider document's text, which logs and debuggers show, never holds its key
public class ChatProviderTests : UnitTestBase
{
    [Fact]
    public void Given_A_Document_Its_Text_Leaves_The_Key_Out()
    {
        // Arrange
        var document = new ChatProvider { Kind = Data.Agent.Provider, ApiKey = Data.Agent.Model };

        // Act
        var text = document.ToString();

        // Assert
        text.Should().NotContain(Data.Agent.Model);
    }

    [Fact]
    public void Given_A_Document_Of_The_Apps_Own_Type_Its_Text_Holds_Its_Own_Members()
    {
        // Arrange
        var document = new CustomerProvider(Data.Agent.ConversationId) { Kind = Data.Agent.Provider };

        // Act
        var text = document.ToString();

        // Assert
        text.Should().Be(
            $"CustomerProvider {{ Kind = {Data.Agent.Provider}, Name = , Endpoint = , Model = , "
            + $"Customer = {Data.Agent.ConversationId} }}");
    }

    [Fact]
    public void Given_Two_Documents_That_Differ_Only_In_Their_Key_They_Are_Two_Providers()
    {
        // Arrange
        var document = new ChatProvider { Kind = Data.Agent.Provider, ApiKey = Data.Agent.Model };

        // Act
        var rotated = document with { ApiKey = Data.Agent.Instructions };

        // Assert
        rotated.Should().NotBe(document);
    }
}
