using FluentAssertions;

namespace Agentic.Tests.Testing.Conventions;

// The InlineData rows mirror BannedCalls' keys
public class TestConventionTests
{
    [Fact]
    public void Every_Test_Is_Named_Given_State_And_Outcome_Or_The_Outcome_Alone()
    {
        // Act
        var violations = ConventionViolations.Names().Except(Exemptions.Names).ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Every_Test_Declaration_Fits_120_Columns()
    {
        // Act
        var violations = ConventionViolations.LongDeclarations().Except(Exemptions.LongDeclarations).ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Every_Test_Class_Lives_In_A_File_Named_After_It_In_Its_Namespace_Folder()
    {
        // Act
        var violations = ConventionViolations.Misplaced().Except(Exemptions.Misplaced).ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Test_Bodies_Carry_No_Comments_But_Arrange_Act_And_Assert()
    {
        // Act
        var violations = ConventionViolations.Comments().Except(Exemptions.Comments).ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Every_Helper_Lives_In_Testing_Or_In_Its_Areas_Support_Folder()
    {
        // Act
        var violations = ConventionViolations.SupportOutsideSupport().Except(Exemptions.SupportOutsideSupport).ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Theory]
    [InlineData("Task.Delay")]
    [InlineData("Thread.Sleep")]
    [InlineData("#region")]
    [InlineData("new Faker")]
    [InlineData("raw cancellation token")]
    [InlineData("numeric wait timeout")]
    [InlineData("wall clock")]
    [InlineData("real time")]
    public void A_Banned_Call_Stays_Out_Of_The_Tests(string call)
    {
        // Act
        var violations = ConventionViolations.Banned(call).Except(Exemptions.BannedCalls).ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Every_Exemption_Still_Applies()
    {
        // Act
        var stale = Exemptions.All.Except(ConventionViolations.All()).ToList();

        // Assert
        stale.Should().BeEmpty(Listing(stale));
    }

    [Fact]
    public void Every_Test_Project_Has_Tests_The_Conventions_Read()
    {
        // Act
        var projects = TestSources.TestMethods.Select(method => TestSources.ProjectOf(method.File)).Distinct();

        // Assert
        projects.Should().BeEquivalentTo("Agentic.Tests", "Agentic.Generators.Tests");
    }

    private static string Listing(IEnumerable<string> entries) =>
        $"every entry breaks the convention or is a stale exemption:{Environment.NewLine}"
        + string.Join(Environment.NewLine, entries);
}
