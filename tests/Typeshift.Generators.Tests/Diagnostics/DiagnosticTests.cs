using System.Reflection;
using Typeshift.Generators.Tests.Diagnostics.Support;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Diagnostics;

public class DiagnosticTests : GeneratorTestBase
{
    public static TheoryData<string> Mistakes { get; } =
    [
        .. typeof(DiagnosticSources)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.Name)
    ];

    [Fact]
    public void Given_A_Toolset_That_Is_Not_Partial_TSH001_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.NotPartial);

        // Assert
        run.Assert.Reports("TSH001", DiagnosticSeverity.Error, "EchoToolset");
    }

    [Fact]
    public void Given_A_Computed_Tool_Name_TSH002_Is_Reported_On_The_Argument()
    {
        // Act
        var run = Run(DiagnosticSources.NotConstant);

        // Assert
        run.Assert.Reports("TSH002", DiagnosticSeverity.Error, "Prefix + \"echo\"");
    }

    [Fact]
    public void Given_A_Lambda_As_Tool_TSH003_Is_Reported_On_The_Argument()
    {
        // Act
        var run = Run(DiagnosticSources.Lambda);

        // Assert
        run.Assert.Reports("TSH003", DiagnosticSeverity.Error, "(Func<string, string>)(text => text)");
    }

    [Fact]
    public void Given_A_Ref_Parameter_TSH004_Is_Reported_On_The_Parameter()
    {
        // Act
        var run = Run(DiagnosticSources.RefParameter);

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "value");
    }

    [Fact]
    public void Given_Settings_That_Are_No_Record_TSH005_Is_Reported_On_WithSettings()
    {
        // Act
        var run = Run(DiagnosticSources.SettingsNotRecord);

        // Assert
        run.Assert.Reports("TSH005", DiagnosticSeverity.Error, "WithSettings<EchoSettings>");
    }

    [Fact]
    public void Given_Internal_Settings_Of_A_Public_Toolset_TSH006_Is_Reported_On_WithSettings()
    {
        // Act
        var run = Run(DiagnosticSources.SettingsLessAccessible);

        // Assert
        run.Assert.Reports("TSH006", DiagnosticSeverity.Error, "WithSettings<EchoSettings>");
    }

    [Fact]
    public void Given_Two_Tools_Mapping_To_One_Member_TSH007_Is_Reported_On_The_Second()
    {
        // Act
        var run = Run(DiagnosticSources.DuplicateName);

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "AddTool(GetOther)");
    }

    [Fact]
    public void Given_Two_Toolsets_With_A_Tool_Of_One_Name_TSH007_Is_Reported_On_The_Second()
    {
        // Act
        var run = Run(DiagnosticSources.DuplicateAcrossToolsets);

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "AddTool(Find)");
    }

    [Fact]
    public void Given_A_Tool_Name_In_Pascal_Case_TSH008_Is_Reported_On_The_Name()
    {
        // Act
        var run = Run(DiagnosticSources.NotLowerSnakeCase);

        // Assert
        run.Assert.Reports("TSH008", DiagnosticSeverity.Error, "\"Echo\"");
    }

    [Fact]
    public void Given_A_Tool_Without_Description_TSH009_Is_Reported_As_A_Warning()
    {
        // Act
        var run = Run(DiagnosticSources.MissingDescription);

        // Assert
        run.Assert.Reports("TSH009", DiagnosticSeverity.Warning, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Tool_That_Does_Not_Take_Its_Settings_TSH010_Is_Reported_As_A_Warning()
    {
        // Act
        var run = Run(DiagnosticSources.UnreadSettings);

        // Assert
        run.Assert.Reports("TSH010", DiagnosticSeverity.Warning, "WithSettings<EchoSettings>");
    }

    [Fact]
    public void Given_A_Summary_Without_Generated_Docs_TSH011_Is_Reported_As_A_Warning()
    {
        // Act
        var run = RunWithoutDocumentation(DiagnosticSources.SummaryOnly);

        // Assert
        run.Assert.Reports("TSH011", DiagnosticSeverity.Warning, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Configurable_Member_Without_Setter_TSH013_Is_Reported_On_The_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.ReadOnlyPick);

        // Assert
        run.Assert.Reports("TSH013", DiagnosticSeverity.Error, "s => s.Twice");
    }

    [Fact]
    public void Given_A_Tool_Declared_In_A_Helper_Method_TSH014_Is_Reported_On_AddTool()
    {
        // Act
        var run = Run(DiagnosticSources.OutsideConstructor);

        // Assert
        run.Assert.Reports("TSH014", DiagnosticSeverity.Error, "AddTool");
    }

    [Fact]
    public void Given_A_Tool_Declared_Under_A_Condition_TSH014_Is_Reported_On_AddTool()
    {
        // Act
        var run = Run(DiagnosticSources.ConditionalTool);

        // Assert
        run.Assert.Reports("TSH014", DiagnosticSeverity.Error, "AddTool");
    }

    [Fact]
    public void Given_A_Generic_Method_As_Tool_TSH003_Is_Reported_On_The_Argument()
    {
        // Act
        var run = Run(DiagnosticSources.GenericMethod);

        // Assert
        run.Assert.Reports("TSH003", DiagnosticSeverity.Error, "Echo<int>");
    }

    [Fact]
    public void Given_A_Chain_Continued_On_A_Variable_TSH015_Is_Reported_On_The_Continuation()
    {
        // Act
        var run = Run(DiagnosticSources.ChainOnVariable);

        // Assert
        run.Assert.ReportsAmong("TSH015", DiagnosticSeverity.Error, "tool.WithDescription(\"Echoes.\")");
    }

    [Fact]
    public void Given_A_Tool_Chain_Stored_In_A_Variable_TSH015_Is_Reported_On_AddTool()
    {
        // Act
        var run = Run(DiagnosticSources.StoredChain);

        // Assert
        run.Assert.Reports("TSH015", DiagnosticSeverity.Error, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Pick_That_Computes_TSH015_Is_Reported_On_The_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.ComputedPick);

        // Assert
        run.Assert.Reports("TSH015", DiagnosticSeverity.Error, "s => s.Repeat + 1");
    }

    [Fact]
    public void Given_A_Private_Nested_Toolset_TSH016_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.PrivateNested);

        // Assert
        run.Assert.Reports("TSH016", DiagnosticSeverity.Error, "EchoToolset");
    }

    [Fact]
    public void Given_A_Toolset_Deriving_From_Another_TSH017_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.Derived);

        // Assert
        run.Assert.Reports("TSH017", DiagnosticSeverity.Error, "LoudToolset");
    }

    [Fact]
    public void Given_A_Generic_Toolset_TSH017_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.Generic);

        // Assert
        run.Assert.Reports("TSH017", DiagnosticSeverity.Error, "EchoToolset");
    }

    [Fact]
    public void Given_A_Member_Named_Like_A_Generated_Factory_TSH018_Is_Reported_On_It()
    {
        // Act
        var run = Run(DiagnosticSources.ClashingMember);

        // Assert
        run.Assert.Reports("TSH018", DiagnosticSeverity.Error, "CreateEcho");
    }

    [Theory]
    [MemberData(nameof(Mistakes))]
    public void Given_Any_Mistake_The_Generated_Code_Holds_No_Error(string mistake)
    {
        // Arrange
        var source = (string)typeof(DiagnosticSources).GetField(mistake)!.GetValue(null)!;

        // Act
        var run = Run(source);

        // Assert
        run.Assert.GeneratesValidCode();
    }
}
