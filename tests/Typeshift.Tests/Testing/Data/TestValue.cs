namespace Typeshift.Tests.Testing.Data;

public class TestValue<T>(T @default)
{
    private readonly Dictionary<string, T> _values = [];

    public T Default { get; } = @default;

    public T Updated => _values[TestValueConstants.Updated];

    public static implicit operator T(TestValue<T> value) => value.Default;

    public static implicit operator TestValue<T>(T @default) => new(@default);

    public TestValue<T> SetUpdated(T value)
    {
        _values[TestValueConstants.Updated] = value;
        return this;
    }
}
