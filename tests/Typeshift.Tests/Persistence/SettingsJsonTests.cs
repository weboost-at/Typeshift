using System.Text.Json;
using Typeshift.Generated;
using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Compaction.Support;
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
    public void Given_A_Stored_Provider_Its_Enum_No_Longer_Has_Reading_Throws_Naming_The_Stored_Text()
    {
        // Act
        var read = () => VendorAgent.Settings.Deserialize("""{"provider":"Anthropic","model":"claude"}""");

        // Assert
        read.Should()
            .Throw<JsonException>()
            .WithMessage($"*'{VendorAgent.Settings.Kind}'*provider \"Anthropic\"*no Vendor*");
    }

    [Fact]
    public void Given_A_Stored_Provider_Of_Another_Json_Type_Reading_Throws_Naming_The_Stored_Text()
    {
        // Act
        var read = () => ProbeAgent.Settings.Deserialize("""{"provider":42}""");

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*provider 42*no string*");
    }

    [Fact]
    public void Given_A_Stored_Summary_Provider_That_Does_Not_Read_Reading_Throws_Naming_It()
    {
        // Act
        var read = () => CompactingAgent.Settings.Deserialize("""{"summaryProvider":["openai"]}""");

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*summaryProvider [\"openai\"]*");
    }

    [Fact]
    public void Given_A_Stored_Provider_Of_Null_It_Reads_As_Absent()
    {
        // Act
        var read = VendorAgent.Settings.Deserialize("""{"provider":null,"model":"claude"}""");

        // Assert
        read.Should().Be(new VendorAgent.Settings { Model = "claude" });
    }

    [Fact]
    public void Given_A_Provider_That_Does_Not_Read_System_Text_Json_Refuses_The_Settings_Too()
    {
        // Act
        var read = () => JsonSerializer.Deserialize<VendorAgent.Settings>("""{"provider":"Anthropic"}""");

        // Assert
        read.Should().Throw<JsonException>().WithMessage("*provider \"Anthropic\"*");
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
    public void Given_A_Stored_Document_With_A_Renamed_Member_Deserialize_Reads_The_Others_And_Leaves_It_Out()
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize(
            """{"$schemaVersion":2,"model":"m","temperature":0.4,"tools":{"inspect":{"count":3,"colour":"Dark"}}}""");

        // Assert
        read.Should().BeEquivalentTo(new EveryKindAgent.Settings
        {
            Model = "m",
            Temperature = 0.4f,
            Inspect = new() { Count = 3 }
        });
    }

    [Fact]
    public void Given_A_Stored_Document_With_A_Renamed_Member_System_Text_Json_Refuses_It_Naming_The_Member()
    {
        // Act
        var read = () => JsonSerializer.Deserialize<EveryKindAgent.Settings>(
            """{"$schemaVersion":2,"model":"m","temperature":0.4,"tools":{"inspect":{"count":3,"colour":"Dark"}}}""");

        // Assert
        read.Should().Throw<JsonException>().WithMessage("'tools.inspect.colour' is no settings member*");
    }

    [Theory]
    [InlineData("""{"Mode":"Auto"}""")]
    [InlineData("""{"mode":"Automatic"}""")]
    [InlineData("""{"maxToolIterations":"7"}""")]
    [InlineData("""{"mod":"Auto"}""")]
    public void Given_A_Request_Body_With_A_Member_That_Does_Not_Read_Binding_Throws(string body)
    {
        // Act
        var bind = () => JsonSerializer.Deserialize<GuardedAgent.Settings>(body, JsonSerializerOptions.Web);

        // Assert
        bind.Should().Throw<JsonException>();
    }

    [Fact]
    public void Given_A_Request_Body_With_A_Member_In_Another_Case_Binding_Throws_Naming_It_In_Camel_Case()
    {
        // Act
        var bind = () => JsonSerializer.Deserialize<GuardedAgent.Settings>("""{"Mode":"Auto"}""");

        // Assert
        bind.Should()
            .Throw<JsonException>()
            .WithMessage("'Mode' is no settings member: member names are camelCase, such as 'mode'.");
    }

    [Fact]
    public void Given_A_Request_Body_With_A_Member_The_Settings_Lack_Binding_Throws_Naming_Their_Members()
    {
        // Act
        var bind = () => JsonSerializer.Deserialize<GuardedAgent.Settings>("""{"mod":"Auto"}""");

        // Assert
        bind.Should()
            .Throw<JsonException>()
            .WithMessage(
                "'mod' is no settings member. The members of the settings are 'mode', 'maxToolIterations' and "
                + "'maxInputTokens'.");
    }

    [Fact]
    public void Given_A_Request_Body_With_A_Value_Of_Another_Type_Binding_Throws_Naming_The_Type()
    {
        // Act
        var bind = () => JsonSerializer.Deserialize<GuardedAgent.Settings>("""{"maxToolIterations":"7"}""");

        // Assert
        bind.Should().Throw<JsonException>().WithMessage("""'maxToolIterations' holds "7", which is no int.""");
    }

    [Fact]
    public void Given_A_Request_Body_With_A_Name_The_Enum_Lacks_Binding_Throws_Naming_Its_Names()
    {
        // Act
        var bind = () => JsonSerializer.Deserialize<GuardedAgent.Settings>("""{"mode":"Automatic"}""");

        // Assert
        bind.Should()
            .Throw<JsonException>()
            .WithMessage(
                """'mode' holds "Automatic", which is no AgentMode. Its names are 'Default', 'Plan' and 'Auto'.""");
    }

    [Theory]
    [InlineData("""{"stopSequences":"END"}""", "'stopSequences'")]
    [InlineData("""{"reasoning":{"effort":"Highest"}}""", "'reasoning.effort'")]
    [InlineData("""{"reasoning":{"depth":1}}""", "'reasoning.depth'")]
    [InlineData("""{"tools":{"inspekt":{}}}""", "'tools.inspekt'")]
    [InlineData("""{"tools":{"inspect":{"Count":3}}}""", "'tools.inspect.Count'")]
    [InlineData("""{"tools":{"inspect":{"span":"30"}}}""", "'tools.inspect.span'")]
    [InlineData("""{"tools":{"inspect":{"address":{"town":"Graz"}}}}""", "'tools.inspect.address.town'")]
    [InlineData("""{"tools":{"inspect":{"home":{"latitude":"north"}}}}""", "'tools.inspect.home.latitude'")]
    [InlineData("""{"tools":{"inspect":{"tags":["a",1]}}}""", "'tools.inspect.tags[1]'")]
    [InlineData("""{"tools":{"inspect":{"tags":["a",null]}}}""", "'tools.inspect.tags[1]'")]
    [InlineData("""{"tools":{"inspect":{"history":[{"city":7}]}}}""", "'tools.inspect.history[0].city'")]
    [InlineData("""{"tools":{"inspect":{"scores":{"a":"1"}}}}""", "'tools.inspect.scores.a'")]
    [InlineData("""{"tools":{"inspect":{"places":{"Purple":{}}}}}""", "'Purple' of 'tools.inspect.places'")]
    public void Given_A_Request_Body_With_A_Nested_Value_That_Does_Not_Read_Binding_Throws_Naming_It(
        string body,
        string named)
    {
        // Act
        var bind = () => JsonSerializer.Deserialize<EveryKindAgent.Settings>(body);

        // Assert
        bind.Should().Throw<JsonException>().WithMessage($"*{named}*");
    }

    [Fact]
    public void Given_A_Request_Body_With_Nulls_Binding_Reads_Them_As_Missing()
    {
        // Act
        var bound = JsonSerializer.Deserialize<EveryKindAgent.Settings>(
            """{"temperature":null,"stopSequences":null,"tools":{"inspect":{"address":null,"tags":null}}}""");

        // Assert
        bound.Should().BeEquivalentTo(new EveryKindAgent.Settings());
    }

    [Fact]
    public void Given_A_Tools_Overrides_Bound_Alone_Binding_Refuses_A_Member_They_Lack()
    {
        // Act
        var bind = () => JsonSerializer.Deserialize<EveryKindToolset.InspectOverrides>("""{"cuont":3}""");

        // Assert
        bind.Should().Throw<JsonException>().WithMessage("'cuont' is no settings member*");
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
    public void Given_A_Finite_Number_Stored_As_A_String_It_Reads_As_Null()
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize("""{"tools":{"inspect":{"precise":"1.5"}}}""");

        // Assert
        read.Inspect.Precise.Should().BeNull();
    }

    [Fact]
    public void Given_A_Relative_Uri_It_Reads_Back()
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize("""{"tools":{"inspect":{"link":"docs/start"}}}""");

        // Assert
        read.Inspect.Link.Should().Be(new Uri("docs/start", UriKind.Relative));
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
    public void Given_A_Negative_Dictionary_Key_Without_A_Name_Its_Entry_Reads_Back_In_Every_Culture()
    {
        // Arrange
        using var culture = NumberCulture.WithMinusSign();
        var places = new Dictionary<Shade, Address> { [(Shade)(-5)] = new() { Street = "Ring 1" } };
        var settings = new EveryKindAgent.Settings { Inspect = new() { Places = places } };

        // Act
        var read = EveryKindAgent.Settings.Deserialize(settings.Serialize());

        // Assert
        read.Inspect.Places.Should().BeEquivalentTo(places);
    }

    [Fact]
    public void Given_A_Negative_Dictionary_Key_Without_A_Name_It_Is_Stored_As_Its_Number_In_Every_Culture()
    {
        // Arrange
        using var culture = NumberCulture.WithMinusSign();
        var places = new Dictionary<Shade, Address> { [(Shade)(-5)] = new() { Street = "Ring 1" } };
        var settings = new EveryKindAgent.Settings { Inspect = new() { Places = places } };

        // Act
        var json = settings.Serialize();

        // Assert
        json.Should().Contain("""{"places":{"-5":{"street":"Ring 1","city":"Vienna"}}}""");
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
    [InlineData("\" Email\"")]
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

    [Theory]
    [InlineData("30")]
    [InlineData("1:30")]
    [InlineData("0:00:30")]
    [InlineData("00:00:30.5")]
    public void Given_A_Time_Span_In_Another_Form_Than_Settings_Write_It_Reads_As_Null(string span)
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize("""{"tools":{"inspect":{"span":""" + $"\"{span}\"" + "}}}");

        // Assert
        read.Inspect.Span.Should().BeNull();
    }

    [Fact]
    public void Given_A_Time_Span_In_Another_Form_Than_Settings_Write_Reading_It_Yields_No_Value()
    {
        // Act
        StorableJson.TryRead(JsonElement.Parse("\"30\""), out TimeSpan span);

        // Assert
        span.Should().Be(default);
    }

    [Theory]
    [InlineData("00:00:30", 30)]
    [InlineData("1.00:00:00", 86_400)]
    [InlineData("-00:01:00", -60)]
    public void Given_A_Time_Span_Written_By_Hand_As_Settings_Write_It_It_Reads(string span, int seconds)
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize("""{"tools":{"inspect":{"span":""" + $"\"{span}\"" + "}}}");

        // Assert
        read.Inspect.Span.Should().Be(TimeSpan.FromSeconds(seconds));
    }

    [Theory]
    [InlineData("01/02/2026")]
    [InlineData("2 Jan 2026")]
    [InlineData("2026-1-2")]
    public void Given_A_Date_In_Another_Form_Than_Settings_Write_It_Reads_As_Null(string day)
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize("""{"tools":{"inspect":{"day":""" + $"\"{day}\"" + "}}}");

        // Assert
        read.Inspect.Day.Should().BeNull();
    }

    [Theory]
    [InlineData("13:45")]
    [InlineData("13:45:30")]
    [InlineData("1:45 PM")]
    public void Given_A_Time_Of_Day_In_Another_Form_Than_Settings_Write_It_Reads_As_Null(string time)
    {
        // Act
        var read = EveryKindAgent.Settings.Deserialize("""{"tools":{"inspect":{"time":""" + $"\"{time}\"" + "}}}");

        // Assert
        read.Inspect.Time.Should().BeNull();
    }

    [Fact]
    public void Given_A_Null_Item_In_A_List_Of_Records_Saving_Fails_Naming_The_Member()
    {
        // Arrange
        var settings = new EveryKindAgent.Settings { Inspect = new() { History = [null!] } };

        // Act
        var save = () => settings.Serialize();

        // Assert
        save.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("'History' holds a null item, which settings cannot store.");
    }

    [Fact]
    public void Given_A_Null_Item_In_A_List_Of_Strings_Saving_Fails_Naming_The_Member()
    {
        // Arrange
        var settings = new EveryKindAgent.Settings { StopSequences = [Data.Persistence.TextOutsideAscii, null!] };

        // Act
        var save = () => settings.Serialize();

        // Assert
        save.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("'StopSequences' holds a null item, which settings cannot store.");
    }

    [Fact]
    public void Given_A_Null_Value_In_A_Dictionary_Saving_Fails_Naming_The_Member()
    {
        // Arrange
        var places = new Dictionary<Shade, Address> { [Shade.Dark] = null! };
        var settings = new EveryKindAgent.Settings { Inspect = new() { Places = places } };

        // Act
        var save = () => settings.Serialize();

        // Assert
        save.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("'Places' holds a null value, which settings cannot store.");
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
