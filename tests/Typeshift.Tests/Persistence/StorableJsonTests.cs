using System.Text.Json;
using Typeshift.Tests.Persistence.Support;
using FluentAssertions;

namespace Typeshift.Tests.Persistence;

public class StorableJsonTests : UnitTestBase
{
    [Fact]
    public void Given_A_Value_Of_Every_Kind_It_Reads_Back_Equivalent()
    {
        // Arrange
        var json = Data.Storable.EveryKind.Serialize();

        // Act
        var restored = EveryKind.Deserialize(json);

        // Assert
        restored.Should().BeEquivalentTo(Data.Storable.EveryKind);
    }

    [Fact]
    public void Given_Null_Values_They_Are_Left_Out()
    {
        // Act
        var json = new EveryKind().Serialize();

        // Assert
        json.Should().Be("{}");
    }

    [Fact]
    public void Given_An_Enum_It_Is_Stored_By_Name()
    {
        // Act
        var json = new EveryKind { Shade = Shade.Dark }.Serialize();

        // Assert
        json.Should().Be("""{"shade":"Dark"}""");
    }

    [Theory]
    [InlineData("7")]
    [InlineData("1")]
    [InlineData("Grey")]
    public void Given_An_Enum_Value_That_Is_No_Member_Name_The_Member_Reads_As_Null(string value)
    {
        // Act
        var restored = EveryKind.Deserialize($$"""{"shade":"{{value}}"}""");

        // Assert
        restored.Shade.Should().BeNull();
    }

    [Fact]
    public void Given_A_Number_With_An_Exponent_It_Reads_As_That_Number()
    {
        // Act
        var restored = EveryKind.Deserialize("""{"money":1.5E-3}""");

        // Assert
        restored.Money.Should().Be(0.0015m);
    }

    [Fact]
    public void Given_NaN_It_Is_Stored_As_A_String()
    {
        // Act
        var json = new EveryKind { Precise = double.NaN }.Serialize();

        // Assert
        json.Should().Be("""{"precise":"NaN"}""");
    }

    [Fact]
    public void Given_An_Infinity_It_Reads_Back_As_That_Infinity()
    {
        // Arrange
        var json = new EveryKind { Ratio = float.NegativeInfinity }.Serialize();

        // Act
        var restored = EveryKind.Deserialize(json);

        // Assert
        restored.Ratio.Should().Be(float.NegativeInfinity);
    }

    [Fact]
    public void Given_A_Finite_Number_In_A_String_The_Member_Reads_As_Null()
    {
        // Act
        var restored = EveryKind.Deserialize("""{"precise":"1.5"}""");

        // Assert
        restored.Precise.Should().BeNull();
    }

    [Fact]
    public void Given_A_Json_Null_The_Member_Reads_As_Null()
    {
        // Act
        var restored = EveryKind.Deserialize("""{"count":null}""");

        // Assert
        restored.Count.Should().BeNull();
    }

    [Fact]
    public void Given_A_Value_Of_Another_Kind_The_Member_Reads_As_Null()
    {
        // Act
        var restored = EveryKind.Deserialize("""{"count":"three"}""");

        // Assert
        restored.Count.Should().BeNull();
    }

    [Fact]
    public void Given_Text_That_Is_No_Json_Object_Reading_Throws_A_JsonException()
    {
        // Act
        var read = () => EveryKind.Deserialize("[]");

        // Assert
        read.Should().Throw<JsonException>();
    }
}
