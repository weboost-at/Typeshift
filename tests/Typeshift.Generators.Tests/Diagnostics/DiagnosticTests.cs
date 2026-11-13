using Typeshift.Generators.Tests.Agents.Support;
using Typeshift.Generators.Tests.Diagnostics.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Generators.Tests.Diagnostics;

public class DiagnosticTests : GeneratorTestBase
{
    public static TheoryData<string> Mistakes { get; } = [.. SourceCatalogs.NamesOf(typeof(DiagnosticSources))];

    public static TheoryData<string> Shapes { get; } = [.. SourceCatalogs.NamesOf(typeof(ShapeSources))];

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
        // Act
        var run = Run(DiagnosticSources.PublicAgentOfInternalProviderIdType);

        // Assert
        run.Assert.Reports("TSH006", DiagnosticSeverity.Error, "o => o.ModelId");
        run.Assert.GeneratesValidCode();
    }

    [Fact]
    public void Given_A_Provider_Id_Type_Nested_As_Private_In_The_Agent_TSH006_Is_Reported_On_The_Model_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.PrivateProviderIdType);

        // Assert
        run.Assert.Reports("TSH006", DiagnosticSeverity.Error, "o => o.ModelId");
        run.Assert.GeneratesValidCode();
    }

    [Fact]
    public void Given_A_Provider_Id_Type_Of_One_File_TSH006_Is_Reported_On_The_Model_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.FileProviderIdType);

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
    public void Given_An_Agents_Tool_Named_Like_A_Toolsets_It_Does_Not_Use_TSH007_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.AgentToolNamedLikeAToolsets);

        // Assert
        run.Assert.DoesNotReport("TSH007");
    }

    [Fact]
    public void Given_Two_Agents_With_Tools_Of_One_Name_And_No_Handoff_TSH007_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.AgentToolsOfOneNameWithoutHandoff);

        // Assert
        run.Assert.DoesNotReport("TSH007");
    }

    [Fact]
    public void Given_A_Handoff_Between_Agents_With_Tools_Of_One_Name_TSH007_Is_Reported_On_Both_Tools()
    {
        // Act
        var run = Run(DiagnosticSources.AgentToolsOfOneNameAcrossHandoff);

        // Assert
        run.Assert.ReportsAmong("TSH007", DiagnosticSeverity.Error, "AddTool(FindOrder)");
        run.Assert.ReportsAmong("TSH007", DiagnosticSeverity.Error, "AddTool(FindTicket)");
    }

    [Fact]
    public void Given_A_Sub_Agent_With_A_Tool_Named_Like_Its_Callers_TSH007_Is_Reported_On_Both_Tools()
    {
        // Act
        var run = Run(DiagnosticSources.AgentToolsOfOneNameAcrossSubAgent);

        // Assert
        run.Assert.ReportsAmong("TSH007", DiagnosticSeverity.Error, "AddTool(FindOrder)");
        run.Assert.ReportsAmong("TSH007", DiagnosticSeverity.Error, "AddTool(FindTicket)");
    }

    [Fact]
    public void Given_Agents_Handing_Off_To_Each_Other_With_Tools_Of_One_Name_TSH007_Is_Reported_Once_On_Each_Tool()
    {
        // Act
        var run = Run(DiagnosticSources.AgentToolsOfOneNameInAHandoffCircle);

        // Assert
        run.Assert.ReportsAmong("TSH007", DiagnosticSeverity.Error, "AddTool(FindOrder)");
        run.Assert.ReportsAmong("TSH007", DiagnosticSeverity.Error, "AddTool(FindTicket)");
    }

    [Fact]
    public void Given_An_Agents_Tool_Named_Like_Its_Toolsets_But_For_Underscores_TSH007_Is_Reported_On_The_Own_Tool()
    {
        // Act
        var run = Run(DiagnosticSources.AgentToolLikeItsToolsetsButForUnderscores);

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "AddTool(GetOwn)");
    }

    [Fact]
    public void Given_Handoffs_Reaching_An_Agent_With_A_Toolset_Tool_Of_One_Name_TSH007_Is_Reported_On_The_Own_Tool()
    {
        // Act
        var run = Run(DiagnosticSources.AgentToolLikeAToolsetsAtTheEndOfHandoffs);

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "AddTool(CloseDesk)");
    }

    [Fact]
    public void Given_A_Handoff_To_A_References_Agent_With_A_Tool_Of_One_Name_TSH007_Is_Reported_On_The_Own_Tool()
    {
        // Arrange
        var reference = CompileReference("Help", ReferenceSources.HelpAgentWithOwnTool);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.HelpAgentHandoffWithOwnTool));

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "AddTool(CloseLead)");
    }

    [Fact]
    public void Given_A_Handoff_To_A_References_Agent_With_A_Tool_Of_A_Toolsets_Name_TSH007_Is_Reported_On_The_Agent()
    {
        // Arrange
        var reference = CompileReference("Help", ReferenceSources.HelpAgentWithOwnTool);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.HelpAgentHandoffWithToolset));

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "LeadAgent");
    }

    [Fact]
    public void Given_An_Agents_Tool_Of_An_Instance_Method_TSH003_Is_Reported_On_The_Argument()
    {
        // Act
        var run = Run(DiagnosticSources.AgentToolOfAnInstanceMethod);

        // Assert
        run.Assert.Reports("TSH003", DiagnosticSeverity.Error, "Close");
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
    public void Given_An_Agents_Summary_Without_Generated_Docs_TSH011_Is_Reported_On_The_Agent()
    {
        // Act
        var run = RunWithoutDocumentation(DiagnosticSources.AgentSummaryOnly);

        // Assert
        run.Assert.Reports("TSH011", DiagnosticSeverity.Warning, "HelpAgent");
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
    public void Given_A_Type_That_Is_No_Store_TSH028_Is_Reported_On_WithStore()
    {
        // Act
        var run = Run(DiagnosticSources.NoStore);

        // Assert
        run.Assert.Reports("TSH028", DiagnosticSeverity.Error, "WithStore<Clock>");
    }

    [Fact]
    public void Given_A_Helper_Generic_Over_The_Store_TSH028_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.GenericRegistrationHelpers);

        // Assert
        run.Assert.DoesNotReport("TSH028");
    }

    [Fact]
    public void Given_A_Store_Of_Another_Id_Type_TSH028_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.OtherStoreIdType);

        // Assert
        run.Assert.DoesNotReport("TSH028");
    }

    [Fact]
    public void Given_A_Store_Of_Conversations_Of_Another_Id_Type_TSH028_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.ConversationStoreOfAnotherIdType);

        // Assert
        run.Assert.DoesNotReport("TSH028");
    }

    [Fact]
    public void Given_A_Store_Of_Settings_Alone_TSH028_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.SettingsStoreAlone);

        // Assert
        run.Assert.DoesNotReport("TSH028");
    }

    [Fact]
    public void Given_A_Store_Of_Decisions_Alone_TSH028_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.DecisionStoreAlone);

        // Assert
        run.Assert.DoesNotReport("TSH028");
    }

    [Fact]
    public void Given_A_Store_Of_Agent_Settings_And_Of_Settings_Of_Another_Id_Type_TSH028_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.AgentSettingsStoreWithSettingsOfAnotherIdType);

        // Assert
        run.Assert.DoesNotReport("TSH028");
    }

    [Fact]
    public void Given_A_Store_Held_As_IAgentSettingsStore_TSH028_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.AgentSettingsStoreHeldAsItsInterface);

        // Assert
        run.Assert.DoesNotReport("TSH028");
    }

    [Fact]
    public void Given_A_Store_Of_Agent_Settings_Alone_TSH028_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.AgentSettingsStore);

        // Assert
        run.Assert.DoesNotReport("TSH028");
    }

    [Fact]
    public void Given_A_Type_That_Is_No_Chat_Client_Provider_TSH030_Is_Reported_On_WithChatClientProvider()
    {
        // Act
        var run = Run(DiagnosticSources.NoChatClientProvider);

        // Assert
        run.Assert.Reports("TSH030", DiagnosticSeverity.Error, "WithChatClientProvider<Clients>");
    }

    [Fact]
    public void Given_A_Helper_Generic_Over_The_Chat_Client_Provider_TSH030_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.GenericRegistrationHelpers);

        // Assert
        run.Assert.DoesNotReport("TSH030");
    }

    [Fact]
    public void Given_An_Agent_Of_Int_Providers_And_A_Chat_Client_Provider_Of_Ints_It_Compiles_Cleanly()
    {
        // Act
        var run = Run(DiagnosticSources.ChatClientProviderOfInts);

        // Assert
        run.Assert.CompilesCleanly();
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
    public void Given_A_Sub_Agent_Described_By_Its_Summary_No_TSH023_Is_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.SubAgentDescribedBySummary);

        // Assert
        run.Assert.DoesNotReport("TSH023");
    }

    [Fact]
    public void Given_A_Sub_Agent_Of_A_Reference_Without_A_Description_TSH023_Is_Reported_On_The_Use()
    {
        // Arrange
        var reference = CompileReference("Help", ReferenceSources.UndescribedHelpAgent);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.HelpAgentUse));

        // Assert
        run.Assert.Reports("TSH023", DiagnosticSeverity.Error, "UseAgent<HelpAgent>")
            .GetMessage()
            .Should()
            .Contain("WithDescription");
    }

    [Fact]
    public void Given_A_Sub_Agent_With_A_Tool_Of_Its_Own_That_Needs_Approval_TSH024_Is_Reported_On_The_Use()
    {
        // Act
        var run = Run(DiagnosticSources.SubAgentWithOwnToolNeedingApproval);

        // Assert
        run.Assert.Reports("TSH024", DiagnosticSeverity.Warning, "UseAgent<HelpAgent>")
            .GetMessage()
            .Should()
            .Contain("close");
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
    public void Given_A_Sub_Agent_Of_A_Reference_Offering_A_Tool_That_Needs_Approval_TSH024_Is_Reported_On_The_Use()
    {
        // Arrange
        var reference = CompileReference("Help", ReferenceSources.AskingHelpAgent);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.HelpAgentUse));

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
    public void Given_A_Stream_Whose_List_No_Context_Lists_Under_Native_Aot_TSH027_Is_Reported_On_Its_Tool()
    {
        // Arrange
        var compilation = Compile(DiagnosticSources.StreamWithoutJsonMetadata);

        // Act
        var run = Run(compilation, properties: BuildProperties.PublishingAot());

        // Assert
        run.Assert.Reports("TSH027", DiagnosticSeverity.Error, "AddTool(List)")
            .GetMessage()
            .Should()
            .Contain("global::System.Collections.Generic.List<global::Company>");
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
    public void Given_A_Configurable_Struct_That_Is_Not_A_Record_TSH012_Is_Reported_On_The_Pick()
    {
        // Act
        var run = Run(DiagnosticSources.PlainStructPick);

        // Assert
        run.Assert.Reports("TSH012", DiagnosticSeverity.Error, "s => s.Home");
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

    [Theory]
    [InlineData(nameof(DiagnosticSources.ToolsetUseBeingTyped))]
    [InlineData(nameof(DiagnosticSources.ToolsetUseUnresolvedGlobal))]
    [InlineData(nameof(DiagnosticSources.ToolsetUseUnresolvedShared))]
    public void Given_A_Toolset_Use_Being_Typed_The_Generator_Runs_On(string mistake)
    {
        // Arrange
        var source = SourceCatalogs.Read(typeof(DiagnosticSources), mistake);

        // Act
        var run = Run(source);

        // Assert
        run.Assert.RunsToTheEnd();
        run.Source("EchoToolset.g.cs").Should().Contain("partial class EchoToolset");
    }

    [Theory]
    [InlineData(nameof(DiagnosticSources.ToolsetUseBeingTyped))]
    [InlineData(nameof(DiagnosticSources.ToolsetUseUnresolvedGlobal))]
    [InlineData(nameof(DiagnosticSources.ToolsetUseUnresolvedShared))]
    [InlineData(nameof(DiagnosticSources.AgentUsesBeingTyped))]
    [InlineData(nameof(DiagnosticSources.SettingsBeingTyped))]
    [InlineData(nameof(DiagnosticSources.SettingsPickOfAnUnresolvedType))]
    [InlineData(nameof(DiagnosticSources.SettingOfAnUnresolvedType))]
    [InlineData(nameof(DiagnosticSources.UnresolvedSignature))]
    [InlineData(nameof(DiagnosticSources.StoreBeingTyped))]
    [InlineData(nameof(DiagnosticSources.ChatClientProviderBeingTyped))]
    public void Given_A_Type_That_Does_Not_Resolve_Yet_Only_The_Compiler_Reports_It(string mistake)
    {
        // Arrange
        var source = SourceCatalogs.Read(typeof(DiagnosticSources), mistake);

        // Act
        var run = Run(source);

        // Assert
        run.Assert.ReportsNothingOfTheAnalyzers();
    }

    [Fact]
    public void Given_A_Configurable_Model_Of_A_Provider_Of_More_Than_One_Value_TSH029_Is_Reported_On_It()
    {
        // Act
        var run = Run(DiagnosticSources.UnstorableProvider);

        // Assert
        run.Assert.Reports("TSH029", DiagnosticSeverity.Error, "new ProviderKey(\"openai\")");
    }

    [Fact]
    public void Given_A_Chained_Constructor_Of_A_Provider_Of_More_Than_One_Value_TSH029_Is_Reported_On_It()
    {
        // Act
        var run = Run(DiagnosticSources.ChainedUnstorableProvider);

        // Assert
        run.Assert.Reports("TSH029", DiagnosticSeverity.Error, "key");
    }

    [Fact]
    public void Given_A_Provider_Of_More_Than_One_Value_In_A_Constructor_That_Never_Runs_TSH029_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.UnstorableProviderOfAnotherConstructor);

        // Assert
        run.Assert.DoesNotReport("TSH029");
    }

    [Fact]
    public void Given_A_Provider_Of_More_Than_One_Value_That_Settings_Do_Not_Store_TSH029_Is_Not_Reported()
    {
        // Act
        var run = Run(DiagnosticSources.UnstoredProvider);

        // Assert
        run.Assert.DoesNotReport("TSH029");
    }

    [Fact]
    public void Given_A_File_Local_Toolset_TSH016_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.FileLocalToolset);

        // Assert
        run.Assert.Reports("TSH016", DiagnosticSeverity.Error, "ScratchTools");
    }

    [Fact]
    public void Given_A_File_Local_Agent_TSH016_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.FileLocalAgent);

        // Assert
        run.Assert.ReportsAmong("TSH016", DiagnosticSeverity.Error, "ScratchAgent");
    }

    [Fact]
    public void Given_A_Toolset_Nested_In_A_File_Local_Class_TSH016_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.ToolsetNestedInAFileLocalClass);

        // Assert
        run.Assert.Reports("TSH016", DiagnosticSeverity.Error, "EchoTools");
    }

    [Fact]
    public void Given_Two_File_Local_Toolsets_Of_One_Name_The_Generator_Does_Not_Fail()
    {
        // Act
        var run = Run(DiagnosticSources.FileLocalToolset, DiagnosticSources.FileLocalToolset);

        // Assert
        run.Assert.RunsToTheEnd();
    }

    [Fact]
    public void Given_A_Tool_Method_Of_A_File_Local_Class_TSH003_Is_Reported_On_The_Argument()
    {
        // Act
        var run = Run(DiagnosticSources.FileLocalToolMethod);

        // Assert
        run.Assert.Reports("TSH003", DiagnosticSeverity.Error, "Lookups.LookUp");
    }

    [Fact]
    public void Given_File_Local_Settings_TSH006_Is_Reported_On_WithSettings()
    {
        // Act
        var run = Run(DiagnosticSources.FileLocalSettings);

        // Assert
        run.Assert.Reports("TSH006", DiagnosticSeverity.Error, "WithSettings<ScratchSettings>");
    }

    [Fact]
    public void Given_Settings_Closed_Over_A_File_Local_Type_TSH006_Is_Reported_On_WithSettings()
    {
        // Act
        var run = Run(DiagnosticSources.SettingsOfAFileLocalTypeArgument);

        // Assert
        run.Assert.Reports("TSH006", DiagnosticSeverity.Error, "WithSettings<Paged<Scratch>>");
    }

    [Fact]
    public void Given_A_File_Local_Parameter_Type_Only_The_Compiler_Reports_It()
    {
        // Act
        var run = Run(DiagnosticSources.FileLocalParameterType);

        // Assert
        run.Assert.ReportsNothingOfTheAnalyzers();
    }

    [Fact]
    public void Given_A_Dynamic_Parameter_TSH004_Is_Reported_On_The_Parameter()
    {
        // Act
        var run = Run(DiagnosticSources.DynamicParameter);

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "value");
    }

    [Fact]
    public void Given_A_Parameter_Of_An_Array_Of_Pointers_TSH004_Is_Reported_On_The_Parameter()
    {
        // Act
        var run = Run(CompileUnsafe(DiagnosticSources.PointerArrayParameter));

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "values");
    }

    [Fact]
    public void Given_A_Result_Of_A_Ref_Struct_TSH004_Is_Reported_On_The_Return_Type()
    {
        // Act
        var run = Run(DiagnosticSources.RefStructResult);

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "ReadOnlySpan<char>");
    }

    [Fact]
    public void Given_A_Result_Of_A_Pointer_TSH004_Is_Reported_On_The_Return_Type()
    {
        // Act
        var run = Run(CompileUnsafe(DiagnosticSources.PointerResult));

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "int*");
    }

    [Fact]
    public void Given_A_Result_Of_A_Task_Of_Dynamic_TSH004_Is_Reported_On_The_Return_Type()
    {
        // Act
        var run = Run(DiagnosticSources.DynamicResult);

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "Task<dynamic>");
    }

    [Fact]
    public void Given_A_Result_Of_A_Nullable_Task_TSH004_Is_Reported_On_The_Return_Type()
    {
        // Act
        var run = Run(DiagnosticSources.NullableTaskResult);

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "Task<string>?");
    }

    [Fact]
    public void Given_A_Result_Of_A_Nullable_ValueTask_TSH004_Is_Reported_On_The_Return_Type()
    {
        // Act
        var run = Run(DiagnosticSources.NullableValueTaskResult);

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "ValueTask<string>?");
    }

    [Fact]
    public void Given_A_Result_Of_A_Nullable_Stream_TSH004_Is_Reported_On_The_Return_Type()
    {
        // Act
        var run = Run(DiagnosticSources.NullableStreamResult);

        // Assert
        run.Assert.Reports("TSH004", DiagnosticSeverity.Error, "IAsyncEnumerable<string>?");
    }

    [Fact]
    public void Given_A_Static_Virtual_Member_Of_An_Interface_As_Tool_Only_The_Compiler_Reports_It()
    {
        // Act
        var run = Run(DiagnosticSources.StaticVirtualInterfaceMember);

        // Assert
        run.Assert.ReportsNothingOfTheAnalyzers();
    }

    [Fact]
    public void Given_A_Member_Of_A_Generic_Extension_Block_As_Tool_TSH003_Is_Reported_On_The_Argument()
    {
        // Act
        var run = Run(DiagnosticSources.MemberOfAGenericExtensionBlock);

        // Assert
        run.Assert.Reports("TSH003", DiagnosticSeverity.Error, "List<int>.Grow");
    }

    [Fact]
    public void Given_A_Tool_Name_Ending_In_A_Line_Break_TSH008_Is_Reported_On_The_Name()
    {
        // Act
        var run = Run(DiagnosticSources.NameEndingInALineBreak);

        // Assert
        run.Assert.Reports("TSH008", DiagnosticSeverity.Error, "\"echo\\n\"");
    }

    [Fact]
    public void Given_A_Toolset_Of_A_Reference_Under_An_Extern_Alias_TSH019_Is_Reported_On_UseToolset()
    {
        // Arrange
        var reference = CompileReference("Lib", AgentSources.Tickets).WithAliases(["lib"]);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.AliasedToolsetAgent));

        // Assert
        run.Assert.Reports("TSH019", DiagnosticSeverity.Error, "UseToolset<TicketToolset>");
    }

    [Fact]
    public void Given_A_Toolset_Of_A_Reference_Under_An_Extern_Alias_The_Generated_Code_Holds_No_Error()
    {
        // Arrange
        var reference = CompileReference("Lib", AgentSources.Tickets).WithAliases(["lib"]);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.AliasedToolsetAgent));

        // Assert
        run.Assert.GeneratesValidCode();
    }

    [Fact]
    public void Given_A_Reference_Under_An_Extern_Alias_TSH020_Is_Reported()
    {
        // Arrange
        var reference = CompileReference("Lib", AgentSources.Tickets).WithAliases(["lib"]);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.AliasedToolsetAgent));

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH020", DiagnosticSeverity.Warning).GetMessage().Should().Contain("alias");
    }

    [Fact]
    public void Given_A_Reference_Under_An_Alias_And_The_Global_One_TSH019_Is_Not_Reported()
    {
        // Arrange
        var reference = CompileReference("Lib", AgentSources.Tickets).WithAliases(["global", "lib"]);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.AliasedToolsetAgent));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Two_Tools_Sharing_A_Generated_Member_TSH007_Is_Reported_On_The_Second()
    {
        // Act
        var run = Run(DiagnosticSources.ToolsSharingAGeneratedMember);

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "AddTool(ReportFunction)");
    }

    [Fact]
    public void Given_Two_Tools_Sharing_A_Generated_Member_TSH007_Names_The_Member()
    {
        // Act
        var run = Run(DiagnosticSources.ToolsSharingAGeneratedMember);

        // Assert
        run.Reported.Single(diagnostic => diagnostic.Id == "TSH007")
            .GetMessage()
            .Should()
            .Contain("'CreateReportFunction'");
    }

    [Fact]
    public void Given_Two_Tools_Sharing_A_Tool_List_Property_TSH007_Is_Reported_On_The_Second()
    {
        // Act
        var run = Run(DiagnosticSources.ToolsSharingAToolListProperty);

        // Assert
        run.Assert.Reports("TSH007", DiagnosticSeverity.Error, "AddTool(Check)");
    }

    [Fact]
    public void Given_An_Agent_Named_Like_A_Generated_Member_TSH018_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.AgentNamedLikeAGeneratedMember);

        // Assert
        run.Assert.Reports("TSH018", DiagnosticSeverity.Error, "Settings");
    }

    [Fact]
    public void Given_A_Toolset_Named_Like_A_Tools_Function_TSH018_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.ToolsetNamedLikeAToolsFunction);

        // Assert
        run.Assert.Reports("TSH018", DiagnosticSeverity.Error, "EchoFunction");
    }

    [Fact]
    public void Given_A_Toolset_Named_Like_A_Tools_Factory_TSH018_Is_Reported_On_Its_Name()
    {
        // Act
        var run = Run(DiagnosticSources.ToolsetNamedLikeAToolsFactory);

        // Assert
        run.Assert.Reports("TSH018", DiagnosticSeverity.Error, "CreateTicket");
    }

    [Fact]
    public void Given_A_Language_Version_Below_14_TSH031_Is_Reported_On_The_Compilation()
    {
        // Act
        var run = RunAs(LanguageVersion.CSharp13, AgentSources.Tickets, AgentSources.Support);

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH031", DiagnosticSeverity.Error).GetMessage().Should().Contain("C# 13.0");
    }

    [Fact]
    public void Given_A_Language_Version_Below_14_No_Other_Diagnostic_Of_The_Analyzer_Is_Reported()
    {
        // Act
        var run = RunAs(LanguageVersion.CSharp13, AgentSources.Tickets, AgentSources.Support);

        // Assert
        run.Reported.Where(diagnostic => diagnostic.Id.StartsWith("TSH", StringComparison.Ordinal))
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Given_A_Language_Version_Below_14_Nothing_Is_Generated()
    {
        // Act
        var run = RunAs(LanguageVersion.CSharp13, AgentSources.Tickets, AgentSources.Support);

        // Assert
        run.Result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Given_A_Language_Version_Below_14_In_A_Project_Without_Toolsets_Or_Agents_TSH031_Is_Not_Reported()
    {
        // Act
        var run = RunAs(LanguageVersion.CSharp13, DiagnosticSources.NothingOfTypeshift);

        // Assert
        run.Assert.DoesNotReport("TSH031");
    }

    [Fact]
    public void Given_A_Language_Version_Below_14_In_A_Project_That_Only_Calls_AddTypeshift_TSH031_Is_Reported()
    {
        // Act
        var run = RunAs(LanguageVersion.CSharp13, ReferenceSources.EmptyHost);

        // Assert
        run.Assert.ReportsOnTheCompilation("TSH031", DiagnosticSeverity.Error);
    }

    [Fact]
    public void Given_Language_Version_14_TSH031_Is_Not_Reported()
    {
        // Act
        var run = RunAs(LanguageVersion.CSharp14, AgentSources.Tickets, AgentSources.Support);

        // Assert
        run.Assert.DoesNotReport("TSH031");
    }

    [Theory]
    [MemberData(nameof(Mistakes))]
    public void Given_Any_Mistake_The_Generated_Code_Holds_No_Error(string mistake)
    {
        // Arrange
        var source = SourceCatalogs.Read(typeof(DiagnosticSources), mistake);

        // Act
        var run = Run(source);

        // Assert
        run.Assert.GeneratesValidCode();
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Given_Any_Shape_The_Generated_Code_Holds_No_Error(string shape)
    {
        // Arrange
        var source = SourceCatalogs.Read(typeof(ShapeSources), shape);

        // Act
        var run = Run(source);

        // Assert
        run.Assert.GeneratesValidCode();
    }
}
