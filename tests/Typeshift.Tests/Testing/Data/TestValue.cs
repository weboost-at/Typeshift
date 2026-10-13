namespace Typeshift.Tests.Testing.Data;

public class TestValue<T>(T @default)
{
    private T? _updated;
    private bool _hasUpdated;

    public T Default { get; } = @default;

    public T Updated => _hasUpdated ? _updated! : throw new InvalidOperationException("SetUpdated was never called.");

    public static implicit operator T(TestValue<T> value) => value.Default;

    public TestValue<T> SetUpdated(T value)
    {
        _updated = value;
        _hasUpdated = true;
        return this;
    }
}
