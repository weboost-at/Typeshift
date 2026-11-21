using FluentAssertions;

namespace Typeshift.Tests.Testing.Conventions;

public class TestConventionTests
{
    public static TheoryData<string> BannedCalls { get; } = [.. ConventionViolations.BannedCalls.Keys];

    [Fact]
    public void Every_Test_Is_Named_Given_State_And_Outcome_Or_The_Outcome_Alone()
    {
        // Act
        var violations = ConventionViolations.Names().ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Every_Test_Class_Lives_In_A_File_Named_After_It_In_Its_Namespace_Folder()
    {
        // Act
        var violations = ConventionViolations.Misplaced().ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Test_Bodies_Carry_No_Comments_But_Arrange_Act_And_Assert()
    {
        // Act
        var violations = ConventionViolations.Comments().ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Every_Test_Asserts()
    {
        // Act
        var violations = ConventionViolations.WithoutAssertion().ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Every_Helper_Lives_In_Testing_Or_In_Its_Areas_Support_Folder()
    {
        // Act
        var violations = ConventionViolations.SupportOutsideSupport().ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Theory]
    [MemberData(nameof(BannedCalls))]
    public void A_Banned_Call_Stays_Out_Of_The_Tests(string call)
    {
        // Act
        var violations = ConventionViolations.Banned(call).ToList();

        // Assert
        violations.Should().BeEmpty(Listing(violations));
    }

    [Fact]
    public void Every_Test_Project_Has_Tests_The_Conventions_Read()
    {
        // Act
        var read = TestSources.TestMethods.Select(method => TestSources.ProjectOf(method.File)).Distinct();

        // Assert
        read.Should().BeEquivalentTo(TestSources.Projects);
    }

    private static string Listing(IEnumerable<string> entries) =>
        $"every entry breaks the convention:{Environment.NewLine}"
        + string.Join(Environment.NewLine, entries);
}
