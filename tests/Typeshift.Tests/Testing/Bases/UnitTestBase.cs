using Microsoft.Extensions.Time.Testing;

namespace Typeshift.Tests.Testing.Bases;

public abstract class UnitTestBase : IDisposable
{
    // The one real clock of the tests: a gate nothing opens fails its test after 20 s instead of hanging the suite
#pragma warning disable RS0030
    private readonly CancellationTokenSource _limit = new(TimeSpan.FromSeconds(20), TimeProvider.System);
#pragma warning restore RS0030
    private readonly CancellationTokenSource _linked;

    protected UnitTestBase() =>
        _linked = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, _limit.Token);

    protected TestDataCollection Data { get; } = new(TestFaker.ForCurrentTest());

    protected CancellationToken Ct => _linked.Token;

    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        _linked.Dispose();
        _limit.Dispose();

        GC.SuppressFinalize(this);
    }
}
