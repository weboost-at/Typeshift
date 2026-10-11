using Agentic.Generators.Tests.Diagnostics.Support;
using Agentic.Generators.Tests.Registration.Support;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Tests.Diagnostics;

public class DiagnosticTests : GeneratorTestBase
{
    [Fact]
    public void Given_A_Toolset_That_Is_Not_Partial_AGT001_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.NotPartial);

        // Assert
        run.Assert.Reports("AGT001", DiagnosticSeverity.Error, "EchoToolset");
    }

    [Fact]
    public void Given_A_Tool_Name_That_Is_Not_A_Constant_AGT002_Is_Reported_On_It()
    {
        // Act
        var run = Run(DiagnosticSources.NotConstant);

        // Assert
        run.Assert.Reports("AGT002", DiagnosticSeverity.Error, "Name");
    }

    [Fact]
    public void Given_A_Tool_Without_WithName_AGT003_Is_Reported_On_AddTool()
    {
        // Act
        var run = Run(DiagnosticSources.MissingToolName);

        // Assert
        run.Assert.Reports("AGT003", DiagnosticSeverity.Error, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Lambda_Instead_Of_A_Static_Method_Group_AGT004_Is_Reported_On_It()
    {
        // Act
        var run = Run(DiagnosticSources.NotStaticMethodGroup);

        // Assert
        run.Assert.Reports("AGT004", DiagnosticSeverity.Error, "(Func<string, string>)(text => text)");
    }

    [Fact]
    public void Given_A_Settings_Class_That_Is_Not_A_Record_AGT005_Is_Reported_On_WithSettings()
    {
        // Act
        var run = Run(DiagnosticSources.SettingsNotRecord);

        // Assert
        run.Assert.Reports("AGT005", DiagnosticSeverity.Error, "WithSettings<EchoSettings>");
    }

    [Fact]
    public void Given_Two_Own_Tools_Sharing_A_Name_AGT008_Is_Reported_On_The_First()
    {
        // Act
        var run = Run(RegistrationSources.EchoToolset, RegistrationSources.ShoutToolset);

        // Assert
        run.Assert.Reports("AGT008", DiagnosticSeverity.Error, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Tool_Named_Like_One_Of_A_Reference_AGT008_Is_Reported_On_The_Own_One()
    {
        // Arrange
        var tools = CompileReference("Tools", RegistrationSources.EchoToolset);

        // Act
        var run = Run(Compile("App", [tools], RegistrationSources.ShoutToolset));

        // Assert
        run.Assert.Reports("AGT008", DiagnosticSeverity.Error, "AddTool(Shout)");
    }

    [Fact]
    public void Given_A_Tool_Without_Any_Description_AGT009_Is_Reported_On_AddTool()
    {
        // Act
        var run = Run(DiagnosticSources.MissingDescription);

        // Assert
        run.Assert.Reports("AGT009", DiagnosticSeverity.Warning, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Tool_Taking_Its_Settings_As_A_Parameter_AGT011_Is_Reported_On_The_Parameter()
    {
        // Act
        var run = Run(DiagnosticSources.SettingsAsParameter);

        // Assert
        run.Assert.Reports("AGT011", DiagnosticSeverity.Error, "settings");
    }

    [Fact]
    public void Given_A_Summary_Without_GenerateDocumentationFile_AGT013_Is_Reported_On_AddTool()
    {
        // Act
        var run = RunWithoutDocumentation(DiagnosticSources.DocumentationOff);

        // Assert
        run.Assert.Reports("AGT013", DiagnosticSeverity.Warning, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Reference_Not_Compiled_With_The_Generator_AGT014_Is_Reported_On_AddAgentic()
    {
        // Arrange
        var tools = CompileReferenceWithoutGenerator("Tools", RegistrationSources.EchoToolset);

        // Act
        var run = Run(Compile("App", [tools], RegistrationSources.Host));

        // Assert
        run.Assert.Reports("AGT014", DiagnosticSeverity.Warning, "services.AddAgentic()");
    }

    [Fact]
    public void Given_A_Tool_Name_That_Is_Not_Lower_Snake_Case_AGT015_Is_Reported_On_It()
    {
        // Act
        var run = Run(DiagnosticSources.NotLowerSnakeCase);

        // Assert
        run.Assert.Reports("AGT015", DiagnosticSeverity.Error, "\"EchoText\"");
    }
}
