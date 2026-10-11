using Microsoft.Extensions.Time.Testing;

namespace Agentic.Tests.Testing.Bases;

public abstract class UnitTestBase
{
    protected TestDataCollection Data { get; } = new(TestFaker.ForCurrentTest());

    protected CancellationToken Ct { get; } = TestContext.Current.CancellationToken;

    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero));
}
