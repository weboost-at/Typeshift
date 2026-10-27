using System.Reflection;
using Typeshift.Generators.Tests.Diagnostics.Support;
using FluentAssertions;
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
    public void Given_A_Public_Agent_Offering_Internal_Overrides_TSH006_Is_Reported_On_The_Use()
    {
        // Act
        var run = Run(DiagnosticSources.PublicAgentOfInternalSettings);

        // Assert
        run.Assert.Reports("TSH006", DiagnosticSeverity.Error, "UseToolset<EchoToolset>");
    }

    [Fact]
    public void Given_A_Public_Agent_Of_An_Internal_Provider_Id_Type_TSH006_Is_Reported_On_The_Model_Pick()
    {
        // Arrange
        var compilation = Compile(DiagnosticSources.PublicAgentOfInternalProviderIdType);

        // Act
        var run = Run(compilation, properties: BuildProperties.WithProviderIdType("Demo.Vendor"));

        // Assert
        run.Assert.Reports("TSH006", DiagnosticSeverity.Error, "o => o.ModelId");
        run.Assert.GeneratesValidCode();
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
    public void Given_A_Configurable_Member_Settings_Cannot_Store_TSH012_Is_Reported_On_The_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.UnstorablePick);

        // Assert
        run.Assert.Reports("TSH012", DiagnosticSeverity.Error, "s => s.Pick");
    }

    [Fact]
    public void Given_A_Recursive_Member_Under_A_Deny_List_TSH012_Is_Reported_On_The_Settings_Call()
    {
        // Act
        var run = Run(DiagnosticSources.UnstorableDenyList);

        // Assert
        run.Assert.Reports("TSH012", DiagnosticSeverity.Error, "WithConfigurableSettings<EchoSettings>");
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

    [Fact]
    public void Given_An_Agent_That_Is_Not_Partial_TSH001_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.AgentNotPartial);

        // Assert
        run.Assert.Reports("TSH001", DiagnosticSeverity.Error, "EchoAgent");
    }

    [Fact]
    public void Given_A_Computed_Agent_Name_TSH002_Is_Reported_On_The_Argument()
    {
        // Act
        var run = Run(DiagnosticSources.ComputedAgentName);

        // Assert
        run.Assert.Reports("TSH002", DiagnosticSeverity.Error, "Prefix + \"_agent\"");
    }

    [Fact]
    public void Given_A_Configurable_Chat_Option_Settings_Cannot_Store_TSH012_Is_Reported_On_The_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.UnstorableOption);

        // Assert
        run.Assert.Reports("TSH012", DiagnosticSeverity.Error, "o => o.Tools");
    }

    [Fact]
    public void Given_An_Agent_Declaration_In_A_Helper_Method_TSH014_Is_Reported_On_The_Call()
    {
        // Act
        var run = Run(DiagnosticSources.DeclarationInHelper);

        // Assert
        run.Assert.Reports("TSH014", DiagnosticSeverity.Error, "UseToolset<EchoToolset>");
    }

    [Fact]
    public void Given_An_Agent_Declaration_Under_A_Condition_TSH014_Is_Reported_On_The_Call()
    {
        // Act
        var run = Run(DiagnosticSources.ConditionalDeclaration);

        // Assert
        run.Assert.Reports("TSH014", DiagnosticSeverity.Error, "UseToolset<EchoToolset>");
    }

    [Fact]
    public void Given_A_Toolset_Use_Continued_On_A_Variable_TSH015_Is_Reported_On_The_Continuation()
    {
        // Act
        var run = Run(DiagnosticSources.UseOnVariable);

        // Assert
        run.Assert.ReportsAmong("TSH015", DiagnosticSeverity.Error, "use.Include(t => t.Echo)");
    }

    [Fact]
    public void Given_A_Tool_Pick_That_Computes_TSH015_Is_Reported_On_The_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.ComputedToolPick);

        // Assert
        run.Assert.Reports("TSH015", DiagnosticSeverity.Error, "t => Pick(t)");
    }

    [Fact]
    public void Given_An_Agent_Without_A_Parameterless_Constructor_TSH016_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.AgentWithoutParameterlessConstructor);

        // Assert
        run.Assert.Reports("TSH016", DiagnosticSeverity.Error, "EchoAgent");
    }

    [Fact]
    public void Given_An_Agent_Deriving_From_Another_TSH017_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.DerivedAgent);

        // Assert
        run.Assert.Reports("TSH017", DiagnosticSeverity.Error, "EchoAgent");
    }

    [Fact]
    public void Given_An_Agent_Member_Named_Settings_TSH018_Is_Reported_On_It()
    {
        // Act
        var run = Run(DiagnosticSources.AgentClashingMember);

        // Assert
        run.Assert.Reports("TSH018", DiagnosticSeverity.Error, "Settings");
    }

    [Fact]
    public void Given_A_Toolset_Of_A_Reference_Without_The_Generator_TSH019_Is_Reported_On_Its_Use()
    {
        // Arrange
        var reference = CompileReferenceWithoutGenerator("Plain", ReferenceSources.PlainToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.PlainToolsetAgent));

        // Assert
        run.Assert.Reports("TSH019", DiagnosticSeverity.Error, "UseToolset<PlainToolset>");
    }

    [Fact]
    public void Given_A_Reference_Declaring_Toolsets_Without_The_Generator_TSH020_Is_Reported()
    {
        // Arrange
        var reference = CompileReferenceWithoutGenerator("Plain", ReferenceSources.PlainToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.PlainToolsetAgent));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH020", DiagnosticSeverity.Warning).GetMessage().Should().Contain("Plain");
    }

    [Fact]
    public void Given_A_Tool_Named_Like_One_Of_A_Reference_TSH007_Is_Reported_On_The_Own()
    {
        // Arrange
        var reference = CompileReference("Companies", ReferenceSources.CompanyToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.SameNamedToolset));

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "AddTool(FindCompanies)");
    }

    [Fact]
    public void Given_Two_References_With_A_Tool_Of_One_Name_TSH007_Is_Reported()
    {
        // Arrange
        var companies = CompileReference("Companies", ReferenceSources.CompanyToolset);
        var search = CompileReference("Search", ReferenceSources.SameNamedToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [companies, search]));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH007", DiagnosticSeverity.Error)
            .GetMessage()
            .Should()
            .Contain("find_companies");
    }

    [Fact]
    public void Given_A_Reference_Of_Toolsets_Only_Built_With_Another_Id_Type_TSH021_Is_Not_Reported()
    {
        // Arrange
        var properties = BuildProperties.WithIdType("int");
        var reference = CompileReference("Companies", properties, ReferenceSources.CompanyToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.SameNamedToolset));

        // Assert
        run.Assert.DoesNotReport("TSH021");
    }

    [Fact]
    public void Given_A_Reference_With_Agents_Built_With_Another_Id_Type_TSH021_Is_Reported()
    {
        // Arrange
        var properties = BuildProperties.WithIdType("int");
        var reference = CompileReference(
            "Companies",
            properties,
            ReferenceSources.CompanyToolset,
            ReferenceSources.CompanyAgent);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.SameNamedToolset));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH021", DiagnosticSeverity.Error).GetMessage().Should().Contain("int");
    }

    [Fact]
    public void Given_A_Reference_With_Agents_Built_With_Another_Provider_Id_Type_TSH021_Is_Reported()
    {
        // Arrange
        var properties = BuildProperties.WithProviderIdType("int");
        var reference = CompileReference(
            "Companies",
            properties,
            ReferenceSources.CompanyToolset,
            ReferenceSources.CompanyAgent);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.SameNamedToolset));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH021", DiagnosticSeverity.Error)
            .GetMessage()
            .Should()
            .Contain("TypeshiftProviderIdType");
    }

    [Fact]
    public void Given_Two_Agents_Of_One_Class_Name_TSH022_Is_Reported_On_The_Second()
    {
        // Act
        var run = Run(DiagnosticSources.SharedKind);

        // Assert
        run.Assert.Reports("TSH022", DiagnosticSeverity.Error, "HelpAgent").GetMessage().Should().Contain("Billing");
    }

    [Fact]
    public void Given_Two_References_With_Agents_Of_One_Kind_TSH022_Is_Reported()
    {
        // Arrange
        var billing = CompileReference("Billing", ReferenceSources.BillingHelpAgent);
        var support = CompileReference("Support", ReferenceSources.SupportHelpAgent);

        // Act
        var run = Run(Compile(SubjectAssembly, [billing, support]));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH022", DiagnosticSeverity.Error).GetMessage().Should().Contain("Billing");
    }

    [Fact]
    public void Given_A_Store_Of_Another_Id_Type_TSH028_Is_Reported_On_WithStore()
    {
        // Act
        var run = Run(DiagnosticSources.OtherStoreIdType);

        // Assert
        run.Assert.Reports("TSH028", DiagnosticSeverity.Error, "WithStore<InMemoryTypeshiftStore<int>>");
    }

    [Fact]
    public void Given_A_Model_Of_A_Provider_Of_Another_Type_TSH030_Is_Reported_On_The_Provider()
    {
        // Act
        var run = Run(DiagnosticSources.OtherProviderIdTypeModel);

        // Assert
        run.Assert.Reports("TSH030", DiagnosticSeverity.Error, "42");
    }

    [Fact]
    public void Given_A_Chat_Client_Provider_Of_Another_Type_TSH030_Is_Reported_On_WithChatClientProvider()
    {
        // Act
        var run = Run(DiagnosticSources.OtherProviderIdTypeProvider);

        // Assert
        run.Assert.Reports("TSH030", DiagnosticSeverity.Error, "WithChatClientProvider<NumberedClients>");
    }

    [Fact]
    public void Given_A_Toolset_Used_Twice_TSH025_Is_Reported_On_The_Second_Use()
    {
        // Act
        var run = Run(DiagnosticSources.ToolsetUsedTwice);

        // Assert
        run.Assert.ReportsAmong("TSH025", DiagnosticSeverity.Error, "UseToolset<EchoToolset>");
    }

    [Fact]
    public void Given_A_Sub_Agent_Without_A_Description_TSH023_Is_Reported_On_The_Use()
    {
        // Act
        var run = Run(DiagnosticSources.SubAgentWithoutDescription);

        // Assert
        run.Assert.Reports("TSH023", DiagnosticSeverity.Error, "UseAgent<HelpAgent>")
            .GetMessage()
            .Should()
            .Contain("WithDescription");
    }

    [Fact]
    public void Given_A_Sub_Agent_Offering_A_Tool_That_Needs_Approval_TSH024_Is_Reported_On_The_Use()
    {
        // Act
        var run = Run(DiagnosticSources.SubAgentNeedingApproval);

        // Assert
        run.Assert.Reports("TSH024", DiagnosticSeverity.Warning, "UseAgent<HelpAgent>")
            .GetMessage()
            .Should()
            .Contain("close");
    }

    [Fact]
    public void Given_Two_Agents_Calling_Each_Other_TSH026_Is_Reported_On_Both_Uses()
    {
        // Act
        var run = Run(DiagnosticSources.AgentCircle);

        // Assert
        run.Assert.ReportsAmong("TSH026", DiagnosticSeverity.Error, "UseAgent<TellAgent>");
        run.Assert.ReportsAmong("TSH026", DiagnosticSeverity.Error, "UseAgent<AskAgent>");
    }

    [Fact]
    public void Given_A_Tool_Type_No_Context_Lists_Under_Native_Aot_TSH027_Is_Reported_On_Its_Tool()
    {
        // Arrange
        var compilation = Compile(DiagnosticSources.ToolTypeWithoutJsonMetadata);

        // Act
        var run = Run(compilation, properties: BuildProperties.PublishingAot());

        // Assert
        run.Assert.Reports("TSH027", DiagnosticSeverity.Error, "AddTool(Count)")
            .GetMessage()
            .Should()
            .Contain("global::System.Guid");
    }

    [Fact]
    public void Given_A_Tool_Type_No_Context_Lists_Outside_Native_Aot_TSH027_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.ToolTypeWithoutJsonMetadata);

        // Assert
        run.Assert.DoesNotReport("TSH027");
    }

    [Fact]
    public void Given_A_Stored_Object_With_A_Required_Member_TSH012_Is_Reported_On_The_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.RequiredStoredMember);

        // Assert
        run.Assert.ReportsAmong("TSH012", DiagnosticSeverity.Error, "s => s.Primary");
    }

    [Fact]
    public void Given_A_Tool_Type_Only_A_Private_Context_Lists_Under_Native_Aot_TSH027_Is_Reported()
    {
        // Arrange
        var compilation = Compile(DiagnosticSources.PrivateJsonContext);

        // Act
        var run = Run(compilation, properties: BuildProperties.PublishingAot());

        // Assert
        run.Assert.Reports("TSH027", DiagnosticSeverity.Error, "AddTool(Find)");
    }

    [Fact]
    public void Given_A_Pick_Being_Typed_TSH015_Is_Reported_And_The_Generator_Runs_On()
    {
        // Act
        var run = Run(DiagnosticSources.PickBeingTyped);

        // Assert
        run.Assert.ReportsAmong("TSH015", DiagnosticSeverity.Error, "Include");
        run.Result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Given_An_Id_Type_The_Build_Cannot_Find_TSH029_Is_Reported_On_The_Compilation()
    {
        // Arrange
        var compilation = Compile(DiagnosticSources.ToolTypeWithoutJsonMetadata);

        // Act
        var run = Run(compilation, properties: BuildProperties.WithIdType("TenantId"));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH029", DiagnosticSeverity.Error)
            .GetMessage()
            .Should()
            .Contain("TenantId");
        run.Assert.GeneratesValidCode();
    }

    [Fact]
    public void Given_A_Provider_Id_Type_The_Build_Cannot_Find_TSH029_Is_Reported_On_The_Compilation()
    {
        // Arrange
        var compilation = Compile(DiagnosticSources.ToolTypeWithoutJsonMetadata);

        // Act
        var run = Run(compilation, properties: BuildProperties.WithProviderIdType("ProviderKey"));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH029", DiagnosticSeverity.Error)
            .GetMessage()
            .Should()
            .Contain("TypeshiftProviderIdType names 'ProviderKey', which the build cannot find");
        run.Assert.GeneratesValidCode();
    }

    [Fact]
    public void Given_A_Provider_Id_Type_Of_More_Than_One_Value_TSH029_Is_Reported_On_The_Compilation()
    {
        // Arrange
        var compilation = Compile(DiagnosticSources.ToolTypeWithoutJsonMetadata);

        // Act
        var run = Run(compilation, properties: BuildProperties.WithProviderIdType("System.Version"));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH029", DiagnosticSeverity.Error)
            .GetMessage()
            .Should()
            .Contain("which settings cannot store as one value");
        run.Assert.GeneratesValidCode();
    }

    [Fact]
    public void Given_A_Tool_Named_Like_A_Built_In_One_TSH025_Is_Reported_Only_On_The_Agent_Offering_Both()
    {
        // Act
        var run = Run(DiagnosticSources.BuiltInToolName);

        // Assert
        run.Assert.DoesNotReport("TSH007");
        run.Assert.Reports("TSH025", DiagnosticSeverity.Error, "SearchAgent")
            .GetMessage()
            .Should()
            .Contain("web_search");
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
