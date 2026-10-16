using Typeshift.Generators.Tests.Toolsets.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Toolsets;

public class ToolsetGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_A_Toolset_Its_Overrides_Factories_Functions_And_Tool_List_Are_Generated()
    {
        // Act
        var run = Run(ToolsetSources.Tickets);

        // Assert
        Snapshot.Verify(run.Result);
    }

    [Fact]
    public void Given_A_Toolset_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.Tickets);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Settings_Configurable_But_Some_Only_Those_Others_Are_Overridden()
    {
        // Act
        var run = Run(ToolsetSources.DeniedSettings);

        // Assert
        Snapshot.Verify(run.Result);
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
    public void Given_A_Nested_Toolset_And_A_Class_Named_Like_Its_Flat_Name_Both_Compile()
    {
        // Act
        var run = Run(ToolsetSources.Nested);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Partial_Toolset_Whose_Second_Declaration_Names_The_Base_It_Is_Generated()
    {
        // Act
        var run = Run(ToolsetSources.SplitPartialFirst, ToolsetSources.SplitPartialSecond);

        // Assert
        run.Assert.CompilesCleanly();
        run.Result.GeneratedTrees.Select(tree => Path.GetFileName(tree.FilePath))
            .Should()
            .Contain("Demo.EchoToolset.g.cs");
    }

    [Fact]
    public void Given_Names_Spelled_Like_Keywords_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.KeywordNames);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Names_Of_Members_Of_The_Generated_Types_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.ReservedNames);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Configurable_Settings_With_An_Obsolete_Member_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.ObsoleteSetting);

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
    public void Given_A_Parameter_Documented_Twice_The_Generator_Does_Not_Fail()
    {
        // Act
        var run = Run(ToolsetSources.DocumentedTwice);

        // Assert
        run.Result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Given_A_Private_Tool_Method_The_Generated_Code_Calls_It()
    {
        // Act
        var run = Run(ToolsetSources.PrivateMethod);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Parameters_The_Model_Never_Sees_Their_Missing_Docs_Are_Suppressed()
    {
        // Act
        var run = Run(ToolsetSources.Tickets);

        // Assert
        run.Assert.Suppresses("CS1573");
    }

    [Fact]
    public void Given_An_Async_Method_Its_Tool_Is_Named_Without_The_Suffix()
    {
        // Act
        var run = Run(ToolsetSources.Tickets);

        // Assert
        run.Source("Demo.TicketToolset.g.cs").Should().Contain("public override string Name => \"search_tickets\";");
    }

    [Fact]
    public void Given_A_Schema_Annotations_Class_The_Module_Registers_Its_Docs()
    {
        // Act
        var run = Run(ToolsetSources.Tickets, ToolsetSources.SchemaAnnotations);

        // Assert
        run.Source("Typeshift.Generated.SubjectModule.g.cs")
            .Should()
            .Contain("AddSchemaAnnotations(services, global::Demo.TicketAnnotations.Source);");
    }

    [Fact]
    public void Given_A_Schema_Annotations_Class_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.Tickets, ToolsetSources.SchemaAnnotations);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Schema_Annotations_Class_That_Is_Not_Partial_The_Module_Leaves_It_Out()
    {
        // Act
        var run = Run(ToolsetSources.Tickets, ToolsetSources.SchemaAnnotationsNotPartial);

        // Assert
        run.Source("Typeshift.Generated.SubjectModule.g.cs").Should().NotContain("AddSchemaAnnotations");
    }
}
