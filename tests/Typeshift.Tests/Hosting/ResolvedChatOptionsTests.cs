using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting;

// Clone copies what the options remember by hand, so a property added without its line fails here, not in the one
// run that clones the options before it reads it
public class ResolvedChatOptionsTests : UnitTestBase
{
    private static readonly PropertyInfo[] Remembered =
    [
        .. typeof(ResolvedChatOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.CanWrite)
    ];

    [Fact]
    public void Given_Every_Property_Set_A_Clone_Carries_Each()
    {
        // Arrange
        var options = new ResolvedChatOptions(new ChatOptions());

        foreach (var property in Remembered)
        {
            property.SetValue(options, SampleOf(property.PropertyType));
        }

        // Act
        var clone = options.Clone();

        // Assert
        ValuesOf(clone).Should().Equal(ValuesOf(options));
    }

    private static List<(string Name, object? Value)> ValuesOf(ChatOptions options) =>
        [.. Remembered.Select(property => (property.Name, property.GetValue(options)))];

    // A value no property starts with; a class without a sample of its own is made without running its constructor,
    // since only its reference is compared
    private static object SampleOf(Type type) =>
        type switch
        {
            _ when type == typeof(bool) => true,
            _ when type == typeof(int) => 1,
            _ when type == typeof(string) => nameof(SampleOf),
            _ when type == typeof(ProviderId) => new ProviderId<string>(nameof(ProviderId)),
            _ when type == typeof(StoreId) => new StoreId<string>(nameof(StoreId)),
            _ when type == typeof(RunOptions) => new RunOptions { MaxToolIterations = 1 },
            _ when type == typeof(ChatResponseFormat) => ChatResponseFormat.Json,
            _ when type == typeof(IReadOnlyList<ConversationEntry>) => Array.Empty<ConversationEntry>(),
            { IsClass: true, IsAbstract: false } => RuntimeHelpers.GetUninitializedObject(type),
            _ => throw new InvalidOperationException($"Add a sample of {type} for the property that has it.")
        };
}
