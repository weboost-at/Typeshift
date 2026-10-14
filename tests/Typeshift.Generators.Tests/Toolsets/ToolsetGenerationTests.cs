using Typeshift.Generators.Tests.Toolsets.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Toolsets;

public class ToolsetGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_A_Toolset_Its_Tools_Settings_And_Functions_Are_Generated()
    {
        // Act
        var run = Run(ToolsetSources.WebResearch);

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_A_Toolset_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.WebResearch);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Parameter_Defaults_Of_Every_Kind_They_Are_Generated_As_Typed_Literals()
    {
        // Act
        var run = Run(ToolsetSources.Defaults);

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_Parameter_Defaults_Of_Every_Kind_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.Defaults);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Nested_Toolset_Its_Containing_Class_Is_Reopened()
    {
        // Act
        var run = Run(ToolsetSources.Nested);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Multi_Line_Description_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.MultiLineDescription);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Namespace_Named_Like_A_Keyword_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.KeywordNamespace);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Configurable_Settings_With_An_Obsolete_Member_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.ObsoleteConfigurableSetting);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Settings_Member_Named_Like_A_Keyword_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.AwkwardSettingNames);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Settings_Member_Named_With_An_Acronym_It_Is_Stored_In_Camel_Case()
    {
        // Act
        var run = Run(ToolsetSources.AwkwardSettingNames);

        // Assert
        run.Source("Demo.LinkToolset.g.cs").Should().Contain("StorableJson.Write(writer, \"urlPrefix\", URLPrefix);");
    }

    [Fact]
    public void Given_A_Parameter_Documented_Twice_The_Generator_Does_Not_Fail()
    {
        // Act
        var run = Run(ToolsetSources.DocumentedTwice);

        // Assert
        run.Assert.DoesNotReport("CS8785");
    }

    [Fact]
    public void Given_Names_Of_Members_Of_The_Generated_Types_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.ReservedNames);

        // Assert
        run.Assert.CompilesCleanly();
    }
}
