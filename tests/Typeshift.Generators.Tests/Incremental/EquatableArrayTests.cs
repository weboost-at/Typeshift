using Typeshift.Generators.Models;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Incremental;

// Every model holds its lists as EquatableArray, whose equality decides whether a step of the pipeline is cached
public class EquatableArrayTests
{
    [Fact]
    public void Given_Two_Arrays_Of_The_Same_Items_They_Are_Equal()
    {
        // Arrange
        var first = new[] { "find", "close" }.ToEquatableArray();
        object second = new[] { "find", "close" }.ToEquatableArray();

        // Act
        var equal = first.Equals(second);

        // Assert
        equal.Should().BeTrue();
    }

    [Fact]
    public void Given_Two_Arrays_Of_The_Same_Items_They_Hash_Alike()
    {
        // Arrange
        var first = new[] { "find", "close" }.ToEquatableArray();
        var second = new[] { "find", "close" }.ToEquatableArray();

        // Act
        var hash = first.GetHashCode();

        // Assert
        hash.Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Given_The_Same_Items_In_Another_Order_The_Arrays_Differ()
    {
        // Arrange
        var first = new[] { "find", "close" }.ToEquatableArray();
        object second = new[] { "close", "find" }.ToEquatableArray();

        // Act
        var equal = first.Equals(second);

        // Assert
        equal.Should().BeFalse();
    }

    [Fact]
    public void Given_Another_Kind_Of_List_Of_The_Same_Items_The_Two_Differ()
    {
        // Arrange
        var array = new[] { "find", "close" }.ToEquatableArray();
        object list = new List<string> { "find", "close" };

        // Act
        var equal = array.Equals(list);

        // Assert
        equal.Should().BeFalse();
    }

    [Fact]
    public void Given_An_Array_Never_Filled_It_Equals_An_Empty_One()
    {
        // Arrange
        var never = default(EquatableArray<string>);

        // Act
        var equal = never.Equals(Array.Empty<string>().ToEquatableArray());

        // Assert
        equal.Should().BeTrue();
    }

    [Fact]
    public void Given_An_Array_Read_As_A_Plain_Sequence_It_Yields_Its_Items_In_Order()
    {
        // Arrange
        System.Collections.IEnumerable array = new[] { "find", "close" }.ToEquatableArray();

        // Act
        var items = array.Cast<string>();

        // Assert
        items.Should().Equal("find", "close");
    }
}
