using System.Text.Json;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Hosting.Support;
using Typeshift.Tests.Persistence.Support;
using FluentAssertions;

namespace Typeshift.Tests.Persistence;

public class SettingsJsonTests : UnitTestBase
{
    [Fact]
    public void Given_Settings_Of_Every_Kind_They_Read_Back_Equivalent()
    {
        // Arrange
        var settings = Data.Persistence.EveryKind;

        // Act
        var read = EveryKindAgent.Settings.Deserialize(settings.Serialize());

        // Assert
        read.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void Given_Schema_Version_1_Settings_Without_Values_They_Are_Stored_As_An_Empty_Object()
    {
        // Act
        var json = new ProbeAgent.Settings().Serialize();

        // Assert
        json.Should().Be("{}");
    }

    [Fact]
    public void Given_Settings_System_Text_Json_Writes_What_The_Store_Keeps()
    {
        // Arrange
        var settings = Data.Persistence.EveryKind;

        // Act
        var json = JsonSerializer.Serialize(settings, JsonSerializerOptions.Default);

        // Assert
        json.Should().Be(settings.Serialize());
    }

    [Fact]
    public void Given_Stored_Json_System_Text_Json_Reads_The_Settings()
    {
        // Arrange
        var settings = Data.Persistence.EveryKind;
        var json = settings.Serialize();

        // Act
        var read = JsonSerializer.Deserialize<EveryKindAgent.Settings>(json, JsonSerializerOptions.Default);

        // Assert
        read.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void Given_Settings_Tools_Are_Stored_By_Name_And_Members_In_Camel_Case()
    {
        // Arrange
        var settings = new EveryKindAgent.Settings { Inspect = new() { Count = 3, Shade = Shade.Dark } };

        // Act
        var json = settings.Serialize();

        // Assert
        json.Should().Be("""{"$schemaVersion":2,"tools":{"inspect":{"count":3,"shade":"Dark"}}}""");
    }

    [Fact]
    public void Given_A_Provider_Of_An_Enum_It_Reads_Back()
    {
        // Arrange
        var settings = new VendorAgent.Settings { Provider = Vendor.Other, Model = Data.Agent.Model };

        // Act
        var read = VendorAgent.Settings.Deserialize(settings.Serialize());

        // Assert
        read.Should().Be(settings);
    }

    [Fact]
    public void Given_A_Provider_Of_An_Enum_It_Is_Stored_By_Its_Name()
    {
        // Arrange
        var settings = new VendorAgent.Settings { Provider = Vendor.Other };

        // Act
        var json = settings.Serialize();

        // Assert
        json.Should().Be("""{"provider":"Other"}""");
    }

    [Fact]
    public void Given_A_Mistyped_Member_It_Reads_As_Null()
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize(
            """{"temperature":"warm","tools":{"inspect":{"count":true}}}""");

        // Assert
        read.Should().BeEquivalentTo(new EveryKindAgent.Settings());
    }

    [Fact]
    public void Given_NaN_It_Is_Stored_As_A_String_And_Read_Back()
    {
        // Arrange
        var settings = new EveryKindAgent.Settings { Inspect = new() { Precise = double.NaN } };

        // Act
        var read = EveryKindAgent.Settings.Deserialize(settings.Serialize());

        // Assert
        read.Inspect.Precise.Should().Be(double.NaN);
    }

    [Fact]
    public void Given_A_Flags_Enum_Without_A_Zero_Member_Its_Zero_Reads_Back()
    {
        // Arrange
        var settings = new EveryKindAgent.Settings { Inspect = new() { Channels = 0 } };

        // Act
        var read = EveryKindAgent.Settings.Deserialize(settings.Serialize());

        // Assert
        read.Inspect.Channels.Should().Be((Channels)0);
    }

    [Theory]
    [InlineData((Channels)4)]
    [InlineData(Channels.Email | (Channels)4)]
    [InlineData((Channels)(-4))]
    public void Given_An_Enum_Value_Without_A_Name_It_Reads_Back(Channels channels)
    {
        // Arrange
        var settings = new EveryKindAgent.Settings { Inspect = new() { Channels = channels } };

        // Act
        var read = EveryKindAgent.Settings.Deserialize(settings.Serialize());

        // Assert
        read.Inspect.Channels.Should().Be(channels);
    }

    [Fact]
    public void Given_An_Enum_Value_Without_A_Name_It_Is_Stored_As_Its_Number()
    {
        // Arrange
        var settings = new EveryKindAgent.Settings { Inspect = new() { Channels = (Channels)4 } };

        // Act
        var json = settings.Serialize();

        // Assert
        json.Should().Be("""{"$schemaVersion":2,"tools":{"inspect":{"channels":4}}}""");
    }

    [Fact]
    public void Given_A_Negative_Enum_Value_Without_A_Name_Its_Number_Is_The_Same_In_Every_Culture()
    {
        // Arrange
        using var culture = NumberCulture.WithMinusSign();
        var settings = new EveryKindAgent.Settings { Inspect = new() { Channels = (Channels)(-4) } };

        // Act
        var json = settings.Serialize();

        // Assert
        json.Should().Be("""{"$schemaVersion":2,"tools":{"inspect":{"channels":-4}}}""");
    }

    [Fact]
    public void Given_A_Culture_With_A_Decimal_Comma_Numbers_Are_Stored_And_Read_The_Same()
    {
        // Arrange
        using var culture = NumberCulture.WithDecimalComma();
        var settings = new EveryKindAgent.Settings { Inspect = new() { Ratio = 0.5f, Precise = 1.25, Money = 9.75m } };

        // Act
        var json = settings.Serialize();
        var read = EveryKindAgent.Settings.Deserialize(json);

        // Assert
        json.Should().Be("""{"$schemaVersion":2,"tools":{"inspect":{"ratio":0.5,"precise":1.25,"money":9.75}}}""");
        read.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void Given_A_Dictionary_Key_Without_A_Name_Its_Entry_Reads_Back()
    {
        // Arrange
        var places = new Dictionary<Shade, Address> { [(Shade)5] = new() { Street = "Ring 1" } };
        var settings = new EveryKindAgent.Settings { Inspect = new() { Places = places } };

        // Act
        var read = EveryKindAgent.Settings.Deserialize(settings.Serialize());

        // Assert
        read.Inspect.Places.Should().BeEquivalentTo(places);
    }

    [Fact]
    public void Given_An_Enum_Value_Stored_As_A_String_Of_Digits_It_Reads_Back()
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize("""{"tools":{"inspect":{"channels":"0"}}}""");

        // Assert
        read.Inspect.Channels.Should().Be((Channels)0);
    }

    [Theory]
    [InlineData("\"Fax\"")]
    [InlineData("\"Email, 4\"")]
    [InlineData("1.5")]
    [InlineData("true")]
    public void Given_Enum_Json_That_Is_Neither_Names_Nor_A_Whole_Number_It_Reads_As_Null(string channels)
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize("""{"tools":{"inspect":{"channels":""" + channels + "}}}");

        // Assert
        read.Inspect.Channels.Should().BeNull();
    }

    [Fact]
    public void Given_A_Stored_Object_Its_Missing_Members_Take_Their_Defaults()
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize(
            """{"tools":{"inspect":{"address":{"street":"Ring 1"}}}}""");

        // Assert
        read.Inspect.Address.Should().Be(new Address { Street = "Ring 1" });
    }

    [Fact]
    public void Given_A_Schema_Version_Newer_Than_The_Codes_Reading_Throws_A_JsonException()
    {
        // Arrange
        var json = $$"""{"$schemaVersion":{{EveryKindAgent.Settings.SchemaVersion + 1}}}""";

        // Act
        var read = () => EveryKindAgent.Settings.Deserialize(json);

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*every_kind*");
    }

    [Fact]
    public void Given_A_Schema_Version_Newer_Than_The_Codes_System_Text_Json_Throws_A_JsonException()
    {
        // Arrange
        var json = $$"""{"$schemaVersion":{{EveryKindAgent.Settings.SchemaVersion + 1}}}""";

        // Act
        var read = () => JsonSerializer.Deserialize<EveryKindAgent.Settings>(json);

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*every_kind*");
    }

    [Fact]
    public void Given_A_Schema_Version_That_Is_No_Whole_Number_Reading_Throws_A_JsonException()
    {
        // Act
        var read = () => EveryKindAgent.Settings.Deserialize("""{"$schemaVersion":"1"}""");

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*whole number*");
    }

    [Fact]
    public void Given_WithSchemaVersion_The_Json_Carries_The_Schema_Version()
    {
        // Act
        var json = new EveryKindAgent.Settings().Serialize();

        // Assert
        json.Should().Be("""{"$schemaVersion":2}""");
    }

    [Fact]
    public void Given_WithStorageKind_And_WithSchemaVersion_The_Settings_Carry_Them()
    {
        // Act
        var stored = (EveryKindAgent.Settings.Kind, EveryKindAgent.Settings.SchemaVersion);

        // Assert
        stored.Should().Be(("every_kind", 2));
    }
}
