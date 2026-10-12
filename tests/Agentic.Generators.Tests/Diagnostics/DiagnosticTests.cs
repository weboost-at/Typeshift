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
    public void Given_An_Agent_That_Is_Not_Partial_AGT001_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.AgentNotPartial);

        // Assert
        run.Assert.Reports("AGT001", DiagnosticSeverity.Error, "EchoAgent");
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
    public void Given_A_Tool_Of_Another_Toolset_Selecting_It_Does_Not_Compile()
    {
        // Act
        var run = Run(DiagnosticSources.ForeignTool);

        // Assert
        run.Assert.DoesNotCompile("CS9286", "t.Shout");
    }

    [Fact]
    public void Given_A_Toolset_Not_Compiled_With_The_Generator_AGT006_Is_Reported_On_UseToolset()
    {
        // Arrange
        var tools = CompileReferenceWithoutGenerator("Tools", DiagnosticSources.UngeneratedToolset);

        // Act
        var run = Run(Compile("App", [tools], DiagnosticSources.AgentOfUngeneratedToolset));

        // Assert
        run.Assert.Reports("AGT006", DiagnosticSeverity.Error, "UseToolset<EchoToolset>");
    }

    [Fact]
    public void Given_Two_Own_Tools_Sharing_A_Name_AGT007_Is_Reported_On_The_First()
    {
        // Act
        var run = Run(RegistrationSources.EchoToolset, RegistrationSources.ShoutToolset);

        // Assert
        run.Assert.Reports("AGT007", DiagnosticSeverity.Error, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Tool_Named_Like_One_Of_A_Reference_AGT007_Is_Reported_On_The_Own_One()
    {
        // Arrange
        var tools = CompileReference("Tools", RegistrationSources.EchoToolset);

        // Act
        var run = Run(Compile("App", [tools], RegistrationSources.ShoutToolset));

        // Assert
        run.Assert.Reports("AGT007", DiagnosticSeverity.Error, "AddTool(Shout)");
    }

    [Fact]
    public void Given_A_Tool_Without_Any_Description_AGT008_Is_Reported_On_AddTool()
    {
        // Act
        var run = Run(DiagnosticSources.MissingDescription);

        // Assert
        run.Assert.Reports("AGT008", DiagnosticSeverity.Warning, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Tool_Without_A_Parameter_For_Its_Settings_AGT009_Is_Reported_On_WithSettings()
    {
        // Act
        var run = Run(DiagnosticSources.UnreadSettings);

        // Assert
        run.Assert.Reports("AGT009", DiagnosticSeverity.Warning, "WithSettings<EchoSettings>");
    }

    [Fact]
    public void Given_Settings_Of_A_Tool_The_Agent_Does_Not_Offer_Configuring_Them_Does_Not_Compile()
    {
        // Act
        var run = Run(DiagnosticSources.SettingsOfAnUnofferedTool);

        // Assert
        run.Assert.DoesNotCompile("CS0103", "ConfigureShout");
    }

    [Fact]
    public void Given_A_Summary_Without_GenerateDocumentationFile_AGT010_Is_Reported_On_AddTool()
    {
        // Act
        var run = RunWithoutDocumentation(DiagnosticSources.DocumentationOff);

        // Assert
        run.Assert.Reports("AGT010", DiagnosticSeverity.Warning, "AddTool(Echo)");
    }

    [Fact]
    public void Given_A_Description_And_A_Summary_Without_GenerateDocumentationFile_AGT010_Is_Not_Reported()
    {
        // Act
        var run = RunWithoutDocumentation(DiagnosticSources.DescribedWithDocumentationOff);

        // Assert
        run.Assert.DoesNotReport("AGT010");
    }

    [Fact]
    public void Given_A_Reference_Not_Compiled_With_The_Generator_AGT011_Is_Reported_On_AddAgentic()
    {
        // Arrange
        var tools = CompileReferenceWithoutGenerator("Tools", RegistrationSources.EchoToolset);

        // Act
        var run = Run(Compile("App", [tools], RegistrationSources.Host));

        // Assert
        run.Assert.Reports("AGT011", DiagnosticSeverity.Warning, "services.AddAgentic()");
    }

    [Fact]
    public void Given_A_Tool_Name_That_Is_Not_Lower_Snake_Case_AGT012_Is_Reported_On_It()
    {
        // Act
        var run = Run(DiagnosticSources.NotLowerSnakeCase);

        // Assert
        run.Assert.Reports("AGT012", DiagnosticSeverity.Error, "\"EchoText\"");
    }

    [Fact]
    public void Given_Internal_Settings_Of_A_Public_Toolset_AGT013_Is_Reported_On_WithSettings()
    {
        // Act
        var run = Run(DiagnosticSources.InternalSettingsOfPublicToolset);

        // Assert
        run.Assert.Reports("AGT013", DiagnosticSeverity.Error, "WithSettings<EchoSettings>");
    }

    [Fact]
    public void Given_An_Unstorable_Configurable_Setting_AGT014_Is_Reported_On_Configurable()
    {
        // Act
        var run = Run(DiagnosticSources.UnsupportedSetting);

        // Assert
        run.Assert.Reports("AGT014", DiagnosticSeverity.Error, "Configurable");
    }

    [Fact]
    public void Given_Configurable_Settings_With_An_Unstorable_Member_AGT014_Is_Reported_On_Them()
    {
        // Act
        var run = Run(DiagnosticSources.UnsupportedConfigurableSetting);

        // Assert
        run.Assert.Reports("AGT014", DiagnosticSeverity.Error, "WithConfigurableSettings<EchoSettings>");
    }

    [Fact]
    public void Given_Configurable_Settings_Keeping_The_Unstorable_Member_AGT014_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.UnsupportedSettingNotConfigurable);

        // Assert
        run.Assert.DoesNotReport("AGT014");
    }

    [Fact]
    public void Given_An_Unstorable_Configurable_Chat_Option_AGT014_Is_Reported_On_Configurable()
    {
        // Act
        var run = Run(DiagnosticSources.UnsupportedChatOption);

        // Assert
        run.Assert.Reports("AGT014", DiagnosticSeverity.Error, "Configurable");
    }
}
