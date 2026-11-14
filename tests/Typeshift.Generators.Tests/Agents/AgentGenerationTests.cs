using Typeshift.Generators.Emitting;
using Typeshift.Generators.Tests.Agents.Support;
using Typeshift.Generators.Tests.Diagnostics.Support;
using Typeshift.Generators.Tests.Toolsets.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Agents;

public class AgentGenerationTests : GeneratorTestBase
{
    [Fact]
    public void Given_An_Agent_Its_Settings_Declaration_Host_Members_Module_And_Interceptor_Are_Generated()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support);

        // Assert
        Snapshot.Verify(
            run.Result,
            [
                "Demo.SupportAgent.g.cs",
                "Demo.SupportAgent+Host.g.cs",
                "Typeshift.Generated.SubjectModule.g.cs",
                "Typeshift.Generated.AddTypeshiftInterceptor.g.cs"
            ]);
    }

    [Fact]
    public void Given_An_Agent_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_An_AddTypeshift_Call_It_Is_Intercepted()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support);

        // Assert
        run.Assert.Intercepts("services.AddTypeshift()");
    }

    [Fact]
    public void Given_A_Caller_Outside_The_Agents_Namespace_Its_Host_Still_Offers_The_Typed_Members()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support, AgentSources.CallerOutsideTheAgentsNamespace);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Caller_In_The_Agents_Namespace_Without_Typeshift_Its_Host_Offers_The_Typed_Members()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support, AgentSources.CallerInTheAgentsNamespace);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_An_Agent_With_A_Nested_Type_Named_Host_Its_Host_Members_Are_Generated()
    {
        // Act
        var run = Run(AgentSources.NestedHost);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Two_Agents_Whose_Namespaces_Join_Alike_Each_Gets_Host_Members_Of_Its_Own()
    {
        // Act
        var run = Run(AgentSources.UnderscoredNamespaces);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Tools_Of_The_Agents_Own_They_Are_Generated_In_It_And_Offered_After_The_Toolsets()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.OwnTools);

        // Assert
        Snapshot.Verify(run.Result, ["Demo.DeskAgent.g.cs", "Typeshift.Generated.SubjectModule.g.cs"]);
    }

    [Fact]
    public void Given_Tools_Of_The_Agents_Own_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.OwnTools);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Toolset_Of_A_Reference_The_Agent_Configures_It_And_The_Interceptor_Registers_Both_Modules()
    {
        // Arrange
        var reference = CompileReference("Companies", ReferenceSources.CompanyToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.CompanyAgent));

        // Assert
        Snapshot.Verify(
            run.Result,
            [
                "Research.ResearchAgent.g.cs",
                "Typeshift.Generated.SubjectModule.g.cs",
                "Typeshift.Generated.AddTypeshiftInterceptor.g.cs"
            ]);
    }

    [Fact]
    public void Given_A_TypeshiftRuntime_Create_Call_The_Interceptor_Builds_The_Runtime_With_The_Projects_Module()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support, AgentSources.Runtime);

        // Assert
        var interceptor = run.Source("Typeshift.Generated.AddTypeshiftInterceptor.g.cs");
        interceptor.Should().Contain("public static global::Typeshift.TypeshiftRuntime Create(");
        interceptor.Should().Contain("CreateRuntime(services, AddTypeshift(services), client, configure)");
        interceptor.Should().Contain("Module.Register(services);");
    }

    [Fact]
    public void Given_A_TypeshiftRuntime_Create_Call_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support, AgentSources.Runtime);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Theory]
    [InlineData("TypeshiftRuntime")]
    [InlineData("Typeshift.TypeshiftRuntime")]
    [InlineData("global::Typeshift.TypeshiftRuntime")]
    public void Given_Only_A_TypeshiftRuntime_Create_Call_It_Is_Intercepted(string receiver)
    {
        // Act
        var run = Run(AgentSources.RuntimeOnly(receiver));

        // Assert
        run.Assert.Intercepts($"{receiver}.Create(client)");
    }

    [Fact]
    public void Given_Only_A_TypeshiftRuntime_Create_Call_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.RuntimeOnly("TypeshiftRuntime"));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Theory]
    [InlineData(nameof(ShapeSources.AddTypeshiftAfterANullCheck), ".AddTypeshift()")]
    [InlineData(
        nameof(ShapeSources.AddTypeshiftInItsStaticForm),
        "TypeshiftServiceCollectionExtensions.AddTypeshift(services)")]
    public void Given_An_AddTypeshift_Call_In_Another_Spelling_It_Is_Intercepted(string shape, string call)
    {
        // Arrange
        var source = SourceCatalogs.Read(typeof(ShapeSources), shape);

        // Act
        var run = Run(source);

        // Assert
        run.Assert.Intercepts(call);
    }

    [Fact]
    public void Given_A_Create_Method_Of_The_Projects_Own_The_Generator_Does_Not_Intercept_It()
    {
        // Act
        var run = Run(AgentSources.OwnCreate);

        // Assert
        run.Assert.DoesNotIntercept("Factory.Create(client)");
    }

    [Fact]
    public void Given_A_TypeshiftRuntime_Create_Call_Bound_At_Run_Time_The_Generator_Does_Not_Intercept_It()
    {
        // Act
        var run = Run(AgentSources.DynamicRuntime);

        // Assert
        run.Assert.DoesNotIntercept("TypeshiftRuntime.Create(client)");
    }

    [Fact]
    public void Given_A_Reference_Of_Docs_Only_The_Interceptor_Registers_Its_Module()
    {
        // Arrange
        var reference = CompileReference("Contracts", ToolsetSources.SchemaAnnotations);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.EmptyHost));

        // Assert
        run.Source("Typeshift.Generated.AddTypeshiftInterceptor.g.cs")
            .Should()
            .Contain("global::Typeshift.Generated.ContractsModule.Register(services);");
    }

    [Fact]
    public void Given_A_Project_With_Only_A_Json_Context_Its_Module_Registers_It()
    {
        // Act
        var run = Run(AgentSources.JsonContextOnly);

        // Assert
        run.Source("Typeshift.Generated.SubjectModule.g.cs")
            .Should()
            .Contain("AddJsonContext(services, global::App.AppJsonContext.Default);");
    }

    [Fact]
    public void Given_A_Project_With_Only_A_Json_Context_The_Interceptor_Registers_Its_Module()
    {
        // Act
        var run = Run(AgentSources.JsonContextOnly);

        // Assert
        run.Source("Typeshift.Generated.AddTypeshiftInterceptor.g.cs")
            .Should()
            .Contain("global::Typeshift.Generated.SubjectModule.Register(services);");
    }

    [Fact]
    public void Given_A_Project_With_Only_A_Json_Context_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(AgentSources.JsonContextOnly);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Json_Context_Without_A_Default_The_Module_Leaves_It_Out()
    {
        // Act
        var run = Run(ShapeSources.JsonContextWithoutDefault);

        // Assert
        run.Source("Typeshift.Generated.SubjectModule.g.cs").Should().NotContain("AddJsonContext");
    }

    [Fact]
    public void Given_A_Json_Context_With_A_Default_Field_Of_Its_Own_The_Module_Registers_It()
    {
        // Act
        var run = Run(ShapeSources.JsonContextWithADefaultField);

        // Assert
        run.Source("Typeshift.Generated.SubjectModule.g.cs")
            .Should()
            .Contain("AddJsonContext(services, global::Demo.HandWritten.Default);");
    }

    [Theory]
    [InlineData("CreateOpenFirst", true)]
    [InlineData("CreateCloseFirst", false)]
    [InlineData("CreateOpenSecond", true)]
    [InlineData("CreateCloseSecond", false)]
    [InlineData("CreateOpenThird", false)]
    [InlineData("CreateCloseThird", true)]
    public void Given_Picks_On_A_Receiver_In_Parentheses_Or_With_A_Suppression_The_Agent_Offers_What_They_Say(
        string factory,
        bool offered)
    {
        // Act
        var run = Run(ShapeSources.PicksOnAWrappedReceiver);

        // Assert
        run.Source("Demo.PickAgent.g.cs").Contains(factory).Should().Be(offered);
    }

    [Fact]
    public void Given_A_Toolset_Of_A_Reference_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Arrange
        var reference = CompileReference("Companies", ReferenceSources.CompanyToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.CompanyAgent));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Names_That_Would_Clash_The_Generated_Code_Avoids_Them_And_Compiles()
    {
        // Act
        var run = Run(AgentSources.Clashes);

        // Assert
        run.Assert.CompilesCleanly();
        run.Output.GetTypeByMetadataName("Demo.Outer+WeatherAgent+Settings")!
            .GetMembers("TemperatureTool")
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Given_A_Private_Nested_Agent_It_Gets_No_Host_Members_And_No_Registration()
    {
        // Act
        var run = Run(AgentSources.Private);

        // Assert
        run.Assert.GeneratesValidCode();
        run.Result.GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("+Host.g.cs"));
    }

    [Theory]
    [InlineData("global::Demo.Provider", "Provider.LmStudio")]
    [InlineData("global::System.Guid", "System.Guid.Empty")]
    [InlineData("int", "7")]
    public void Given_A_Model_Of_A_Provider_Of_A_Type_The_Settings_Store_Providers_Of_That_Type(
        string type,
        string provider)
    {
        // Act
        var run = Run(AgentSources.Routed(type, provider));

        var sources = run.Result.GeneratedTrees.Select(tree => tree.ToString()).ToList();

        // Assert
        run.Assert.CompilesCleanly();
        sources.Should().Contain(source => source.Contains($"public {type}? Provider {{ get; init; }}"));
        sources.Should().Contain(source => source.Contains($"public {type}? SummaryProvider {{ get; init; }}"));
    }

    [Theory]
    [InlineData("Demo.VendorAgent+Settings", "Demo.Vendor?")]
    [InlineData("Demo.NamedAgent+Settings", "string?")]
    [InlineData("Demo.PickedAgent+Settings", "string?")]
    public void Given_Agents_Of_Two_Provider_Types_Each_Settings_Store_Providers_Of_Its_Own(
        string settings,
        string provider)
    {
        // Act
        var run = Run(AgentSources.TwoProviderIdTypes);

        // Assert
        run.Assert.CompilesCleanly();
        run.Output.GetTypeByMetadataName(settings)!
            .GetMembers("Provider")
            .OfType<IPropertySymbol>()
            .Single()
            .Type.ToDisplayString()
            .Should()
            .Be(provider);
    }

    [Theory]
    [InlineData("Demo.SplitAgent+Settings", "Demo.Vendor?")]
    [InlineData("Demo.OverridingAgent+Settings", "string?")]
    public void Given_Several_Constructors_The_Settings_Store_Providers_Of_What_The_Parameterless_One_Runs(
        string settings,
        string provider)
    {
        // Act
        var run = Run(AgentSources.ProvidersOfSeveralConstructors);

        // Assert
        run.Assert.CompilesCleanly();
        run.Output.GetTypeByMetadataName(settings)!
            .GetMembers("Provider")
            .OfType<IPropertySymbol>()
            .Single()
            .Type.ToDisplayString()
            .Should()
            .Be(provider);
    }

    [Fact]
    public void Given_An_Experimental_Toolset_Settings_And_Provider_Type_The_Agents_File_Disables_Their_Ids()
    {
        // Act
        var run = Run(ShapeSources.ExperimentalToolsetSettingsAndProviderId);

        // Assert
        run.Source("Demo.TrialAgent.g.cs")
            .Should()
            .Contain("#pragma warning disable DEMO003, DEMO004, DEMO005, DEMO006, DEMO007\n");
    }

    [Fact]
    public void Given_A_Referenced_Toolset_With_Experimental_Settings_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Arrange
        var reference = CompileReference("Trials", ReferenceSources.ExperimentalToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.ExperimentalToolsetAgent));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Settings_Without_A_Settable_Member_The_Agent_Gets_No_Configure_For_The_Tool()
    {
        // Act
        var run = Run(ShapeSources.SettingsWithoutSettableMembers);

        // Assert
        run.Source("Demo.NothingAgent.g.cs").Should().NotContain("ConfigureEcho");
    }

    [Fact]
    public void Given_A_Reference_Under_An_Extern_Alias_The_Interceptor_Leaves_Its_Module_Out()
    {
        // Arrange
        var reference = CompileReference("Lib", AgentSources.Tickets).WithAliases(["lib"]);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.AliasedToolsetAgent));

        // Assert
        run.Source("Typeshift.Generated.AddTypeshiftInterceptor.g.cs").Should().NotContain("LibModule");
    }

    [Fact]
    public void Given_Tools_Named_Like_Members_Of_Object_The_Agents_Settings_Name_Them_With_A_Suffix()
    {
        // Act
        var run = Run(ShapeSources.ToolsNamedLikeMembersOfObject);

        // Assert
        run.Output.GetTypeByMetadataName("Demo.TypeAgent+Settings")!
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Select(property => property.Name)
            .Should()
            .Contain(["GetTypeTool", "ReferenceEqualsTool", "GetHashCodeTool", "ToStringTool"]);
    }

    [Fact]
    public void Given_Two_References_Whose_Names_Differ_In_Punctuation_Their_Modules_Do_Not_Clash()
    {
        // Arrange
        var first = CompileReference("Acme.Tools", ReferenceSources.EchoToolset("First", "FirstTools", "EchoOne"));
        var second = CompileReference("Acme-Tools", ReferenceSources.EchoToolset("Second", "SecondTools", "EchoTwo"));

        // Act
        var run = Run(Compile(SubjectAssembly, [first, second], ReferenceSources.TwoToolsetsHost));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Theory]
    [InlineData("Acme.Tools", "Acme_ToolsModule")]
    [InlineData("Subject", "SubjectModule")]
    [InlineData("4Sales", "_4SalesModule")]
    public void Given_An_Assembly_Name_Of_Letters_Digits_And_Dots_Its_Module_Is_Named_After_It(
        string assembly,
        string module)
    {
        // Act
        var name = ModuleEmitter.GetName(assembly);

        // Assert
        name.Should().Be(module);
    }

    [Theory]
    [InlineData("Acme-Tools", "Acme_Tools_")]
    [InlineData("Acme_Tools", "Acme_Tools_")]
    [InlineData("Acme Tools", "Acme_Tools_")]
    public void Given_An_Assembly_Name_With_Other_Characters_Its_Module_Carries_A_Hash_Of_The_Name(
        string assembly,
        string start)
    {
        // Act
        var name = ModuleEmitter.GetName(assembly);

        // Assert
        name.Should().MatchRegex($"^{start}[0-9a-f]{{8}}Module$");
    }

    [Theory]
    [InlineData(nameof(ShapeSources.ToolsetNamedInFullOrByAnAlias))]
    [InlineData(nameof(ShapeSources.NestedNamespacesAndClasses))]
    [InlineData(nameof(ShapeSources.SameNamesInTwoNamespaces))]
    public void Given_A_Toolset_Of_A_Namespace_The_Agents_File_Does_Not_Import_Its_Picks_Compile(string shape)
    {
        // Arrange
        var source = SourceCatalogs.Read(typeof(ShapeSources), shape);

        // Act
        var run = Run(source);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Toolset_Of_A_Reference_Named_In_Full_Its_Picks_Compile()
    {
        // Arrange
        var reference = CompileReference("Companies", ReferenceSources.CompanyToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.CompanyAgentNamingTheToolsetInFull));

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Settings_Member_With_A_Summary_Its_With_Method_Carries_It()
    {
        // Act
        var run = Run(AgentSources.Tickets, AgentSources.Support);

        // Assert
        run.Source("Demo.SupportAgent.g.cs")
            .Should()
            .Contain("/// Sets <c>Limit</c>: The most tickets a search returns.");
    }

    [Fact]
    public void Given_A_Schema_Version_The_Agents_Settings_Carry_It()
    {
        // Act
        var run = Run(ShapeSources.EveryAgentDeclaration);

        // Assert
        run.Source("Demo.OfficeAgent.g.cs").Should().Contain("public static int SchemaVersion => 3;");
    }

    [Fact]
    public void Given_Two_Models_In_One_Constructor_The_Settings_Store_Providers_Of_The_Last_Ones_Type()
    {
        // Act
        var run = Run(ShapeSources.TwoModelsInOneConstructor);

        // Assert
        run.Output.GetTypeByMetadataName("Demo.SwitchingAgent+Settings")!
            .GetMembers("Provider")
            .OfType<IPropertySymbol>()
            .Single()
            .Type.ToDisplayString()
            .Should()
            .Be("string?");
    }

    [Fact]
    public void Given_Tools_Included_In_Another_Order_Than_The_Toolsets_The_Agent_Offers_Them_As_Picked()
    {
        // Act
        var run = Run(ShapeSources.ToolsIncludedInAnotherOrder);

        // Assert
        run.Source("Typeshift.Generated.SubjectModule.g.cs")
            .Should()
            .Contain("Tools = new string[] { \"send\", \"find\" }");
    }

    [Fact]
    public void Given_An_Obsolete_Setting_That_Is_Not_Configurable_The_Agent_Gets_No_With_Method_For_It()
    {
        // Act
        var run = Run(ReferenceSources.ObsoleteSettingToolset, ReferenceSources.ObsoleteSettingAgent);

        // Assert
        run.Source("Research.EchoAgent.g.cs").Should().NotContain("WithTimes");
    }

    [Fact]
    public void Given_An_Obsolete_Setting_Of_A_Referenced_Toolset_The_Agent_Gets_No_With_Method_For_It()
    {
        // Arrange
        var reference = CompileReference("Echoes", ReferenceSources.ObsoleteSettingToolset);

        // Act
        var run = Run(Compile(SubjectAssembly, [reference], ReferenceSources.ObsoleteSettingAgent));

        // Assert
        run.Source("Research.EchoAgent.g.cs").Should().NotContain("WithTimes");
    }

    [Fact]
    public void Given_A_Toolset_With_An_Internal_Constructor_An_Agent_Of_Its_Assembly_Uses_It()
    {
        // Act
        var run = Run(ShapeSources.ToolsetWithAnInternalConstructor);

        // Assert
        run.Assert.CompilesCleanly();
        run.Source("Demo.ResearchAgent.g.cs").Should().Contain("global::Demo.CompanyToolset.CreateFind(json)");
    }

    [Theory]
    [InlineData(nameof(ShapeSources.AgentWithABaseInitializer))]
    [InlineData(nameof(ShapeSources.AgentWithAPrimaryConstructor))]
    public void Given_An_Explicit_Base_Initializer_The_Agent_Is_Generated(string shape)
    {
        // Arrange
        var source = SourceCatalogs.Read(typeof(ShapeSources), shape);

        // Act
        var run = Run(source);

        // Assert
        run.Assert.DoesNotReport("TSH014");
        run.Assert.CompilesCleanly();
        run.Source("Demo.ProbeAgent.g.cs").Should().Contain("partial class ProbeAgent");
    }
}
