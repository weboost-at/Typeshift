using Typeshift.Generators.Parsing;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

// A change of the public API shows as a diff of Snapshots/PublicApi.verified.txt, which a reviewer reads before it
// is accepted with ACCEPT_SNAPSHOTS=1
public class PublicApiSurfaceTests
{
    [Fact]
    public void The_Public_Surface_Matches_Its_Snapshot()
    {
        // Act
        var surface = PublicApi.Render(PublicApi.Members);

        // Assert
        Snapshot.Verify("PublicApi", surface);
    }

    [Fact]
    public void The_Surface_Generated_Code_Binds_To_Changes_Only_With_The_Stamp_Version()
    {
        // Arrange
        var members = PublicApi.Render(PublicApi.GeneratedContract);
        var contract = $"StampVersion = {ReferenceReader.StampVersion}\n{members}\n";
        var verified = Snapshot.Read("GeneratedContract") ?? contract;

        // Act
        var changedUnderItsVersion = verified != contract && Version(verified) == Version(contract);

        // Assert
        changedUnderItsVersion.Should().BeFalse(
            "D7: a library built before this change binds to the members that changed, so raise "
            + "ReferenceReader.StampVersion, then accept the snapshot");

        Snapshot.Verify("GeneratedContract", contract);
    }

    private static string Version(string contract) => contract[..contract.IndexOf('\n')];
}
