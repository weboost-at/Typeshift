using Typeshift.Generators.Tests.Diagnostics.Support;
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
    public void Given_Settings_Configurable_But_Some_The_Generated_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.DeniedSettings);

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

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("nb-NO")]
    [InlineData("fi-FI")]
    public void Given_A_Build_Machine_Whose_Culture_Writes_Another_Minus_Sign_Negative_Defaults_Compile(
        string culture)
    {
        // Arrange
        using var machine = new BuildCulture(culture);

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
    public void Given_Two_Classes_Whose_Names_Differ_Only_By_Case_Both_Are_Generated()
    {
        // Act
        var run = Run(ShapeSources.NamesThatDifferByCase);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Theory]
    [InlineData("global::Demo.FindToolset.Find(GetArgument<string>(arguments, \"text\"))")]
    [InlineData("global::Demo.FindToolset.Find(GetArgument<int>(arguments, \"number\"))")]
    public void Given_Overloads_Picked_By_A_Cast_Each_Tool_Calls_Its_Method(string call)
    {
        // Act
        var run = Run(ShapeSources.OverloadsPickedByACast);

        // Assert
        run.Assert.CompilesCleanly();
        run.Source("Demo.FindToolset.g.cs").Should().Contain(call);
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

    [Theory]
    [InlineData("\"Converts amount from into EUR DocToolset units.\"")]
    [InlineData("\"The decimal String amount.\"")]
    [InlineData("\"Searches tickets. Returns at most ten.\"")]
    [InlineData("\"One of fast slow\"")]
    [InlineData("\"Reads a page, see https://example.com/pages. Or a file.\"")]
    public void Given_Adjacent_Elements_In_A_Doc_Comment_Their_Texts_Stay_Apart(string description)
    {
        // Act
        var run = Run(ToolsetSources.AdjacentDocElements);

        // Assert
        run.Source("Demo.DocToolset.g.cs").Should().Contain(description);
    }

    [Fact]
    public void Given_A_Multi_Line_Description_The_Tool_Is_Described_On_One_Line()
    {
        // Act
        var run = Run(ToolsetSources.MultiLineDescription);

        // Assert
        run.Source("Demo.NoteToolset.g.cs")
            .Should()
            .Contain("Description => \"Takes a note that spans several lines, with code & \\\"quotes\\\".\";");
    }

    [Fact]
    public void Given_A_Parameter_Documented_Twice_The_Generator_Does_Not_Fail()
    {
        // Act
        var run = Run(ToolsetSources.DocumentedTwice);

        // Assert
        run.Assert.RunsToTheEnd();
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
    public void Given_A_Tool_Method_In_Another_Class_Its_Hidden_Parameters_Need_No_Docs()
    {
        // Act
        var run = Run(ShapeSources.HiddenParametersOfToolsInAnotherClass);

        // Assert
        run.Assert.Suppresses("CS1573");
    }

    [Fact]
    public void Given_A_Helper_That_Is_No_Tool_The_Missing_Docs_Of_Its_Parameters_Are_Reported()
    {
        // Act
        var run = Run(ShapeSources.HelperOfAToolsetThatIsNoTool);

        // Assert
        run.Reported
            .Where(diagnostic => diagnostic.Id == "CS1573")
            .Select(diagnostic => diagnostic.Location.SourceTree!.GetText(Ct).ToString(diagnostic.Location.SourceSpan))
            .Should()
            .Equal("helperToken");
    }

    [Fact]
    public void Given_A_Method_Outside_A_Toolset_The_Missing_Docs_Of_Its_Parameters_Are_Reported()
    {
        // Act
        var run = Run(ToolsetSources.MethodOutsideAToolset);

        // Assert
        run.Reported.Select(diagnostic => diagnostic.Id).Should().Contain("CS1573");
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
    public void Given_A_Schema_Annotations_Class_And_No_Toolsets_Or_Agents_A_Module_Registers_Its_Docs()
    {
        // Act
        var run = Run(ToolsetSources.SchemaAnnotations);

        // Assert
        run.Source("Typeshift.Generated.SubjectModule.g.cs")
            .Should()
            .Contain("AddSchemaAnnotations(services, global::Demo.TicketAnnotations.Source);");
    }

    [Fact]
    public void Given_A_Schema_Annotations_Class_And_No_Toolsets_Or_Agents_The_Module_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ToolsetSources.SchemaAnnotations);

        // Assert
        run.Assert.CompilesCleanly();
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

    [Fact]
    public void Given_Several_Json_Contexts_The_Module_Registers_Them_In_Source_Order()
    {
        // Act
        var run = Run(ShapeSources.SeveralJsonContexts);

        var registered = run.Source("Typeshift.Generated.SubjectModule.g.cs")
            .Split('\n')
            .Where(line => line.Contains("AddJsonContext"))
            .Select(line => line[line.IndexOf("global::Demo", StringComparison.Ordinal)..]);

        // Assert
        registered.Should().Equal(
            "global::Demo.OrderJson.Default);",
            "global::Demo.ListJson.Default);",
            "global::Demo.NumberJson.Default);",
            "global::Demo.TextJson.Default);",
            "global::Demo.FlagJson.Default);");
    }

    [Fact]
    public void Given_Stored_Collections_A_Stream_Tool_And_A_Json_Context_Their_Code_Is_Generated()
    {
        // Act
        var run = Run(ShapeSources.StoredCollectionsAStreamAndAJsonContext);

        // Assert
        Snapshot.Verify(run.Result, ["Demo.CrawlToolset.g.cs", "Typeshift.Generated.SubjectModule.g.cs"]);
    }

    [Fact]
    public void Given_Stored_Collections_A_Stream_Tool_And_A_Json_Context_The_Code_Compiles_Without_Warnings()
    {
        // Act
        var run = Run(ShapeSources.StoredCollectionsAStreamAndAJsonContext);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Namespaces_Named_Like_The_Frameworks_The_Generated_Code_Compiles()
    {
        // Act
        var run = Run(ShapeSources.NamespacesNamedLikeTheFrameworks);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_Experimental_Types_Of_A_Tool_The_Toolsets_File_Disables_Their_Ids_And_No_Others()
    {
        // Act
        var run = Run(ShapeSources.ExperimentalToolTypesAndAgent);

        // Assert
        run.Source("Demo.PreviewTools.g.cs")
            .Should()
            .StartWith(
                "// <auto-generated/>\n#nullable enable\n#pragma warning disable CS0612, CS0618\n"
                + "#pragma warning disable DEMO001\n\n");
    }

    [Fact]
    public void Given_A_Tool_Of_An_Extension_Block_The_Generated_Code_Calls_It_On_The_Class_That_Declares_The_Block()
    {
        // Act
        var run = Run(ShapeSources.MemberOfAnExtensionBlock);

        // Assert
        run.Source("Demo.ShoutTools.g.cs").Should().Contain("new(GetResult(global::Demo.TextExtensions.Shout(");
    }

    [Fact]
    public void Given_A_Tool_Of_A_Class_Closed_Over_A_Nullable_Type_The_Generated_Call_Keeps_The_Annotation()
    {
        // Act
        var run = Run(ShapeSources.ToolOfAClassClosedOverANullableType);

        // Assert
        run.Source("Demo.FindTools.g.cs").Should().Contain("new(GetResult(global::Demo.Finder<string?>.Find(");
    }

    [Fact]
    public void Given_A_Nullable_Cancellation_Token_The_Call_Fills_It_And_The_Model_Is_Not_Asked_For_It()
    {
        // Act
        var run = Run(ShapeSources.NullableParametersTheCallFills);

        // Assert
        run.Source("Demo.WaitTools.g.cs").Should().NotContain("new(\"ct\"");
    }

    [Fact]
    public void Given_A_Stream_Tool_The_Library_Reads_Its_Stream()
    {
        // Act
        var run = Run(ShapeSources.StreamTool);

        // Assert
        run.Source("Demo.WordToolset.g.cs")
            .Should()
            .Contain("GetResultAsync(global::Demo.WordToolset.Spell(GetArgument<string>(arguments, \"word\")), ct);");
    }

    [Fact]
    public void Given_A_Stream_Tool_The_Generated_Code_Names_No_Type_Of_Async_Linq()
    {
        // Act
        var run = Run(ShapeSources.StreamTool);

        // Assert
        run.Source("Demo.WordToolset.g.cs").Should().NotContain("System.Linq");
    }

    [Fact]
    public void Given_A_Toolset_Its_Tool_List_Is_A_Hidden_Class_In_The_Global_Namespace_Named_After_Its_Full_Name()
    {
        // Act
        var run = Run(ToolsetSources.Tickets);

        // Assert
        run.Output.GetTypeByMetadataName("TypeshiftTools_4Demo13TicketToolset")!
            .GetAttributes()
            .Select(attribute => attribute.AttributeClass!.Name)
            .Should()
            .Contain("EditorBrowsableAttribute");
    }

    [Fact]
    public void Given_Types_Named_Like_A_Toolsets_Tool_List_Everything_Compiles()
    {
        // Act
        var run = Run(ShapeSources.TypesNamedLikeAToolsetsToolList);

        // Assert
        run.Assert.CompilesCleanly();
    }

    [Fact]
    public void Given_A_Toolset_Its_Own_Class_Is_Not_Marked_As_Generated_Code()
    {
        // Act
        var run = Run(ToolsetSources.Tickets);

        // Assert
        run.Output.GetTypeByMetadataName("Demo.TicketToolset")!
            .GetAttributes()
            .Select(attribute => attribute.AttributeClass!.Name)
            .Should()
            .NotContain("GeneratedCodeAttribute");
    }

    [Fact]
    public void Given_A_Toolset_The_Generated_Code_Names_The_Generators_Version_Without_Its_Commit()
    {
        // Act
        var run = Run(ToolsetSources.Tickets);

        // Assert
        run.Source("Demo.TicketToolset.g.cs")
            .Should()
            .MatchRegex(@"GeneratedCode\(""Typeshift\.Generators"", ""\d+\.\d+\.\d+[^+""]*""\)");
    }
}
