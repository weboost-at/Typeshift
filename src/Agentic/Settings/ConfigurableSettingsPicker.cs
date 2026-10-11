using System.Linq.Expressions;

namespace Agentic.Settings;

/// <summary>
/// Picks the members of a settings type that may change at runtime, through storage or for a single run.
/// </summary>
public sealed class ConfigurableSettingsPicker<TSettings>
{
    private readonly HashSet<string> _configurable = [];

    internal IReadOnlySet<string> Picked => _configurable;

    /// <summary>
    /// Allows the picked properties to change at runtime; every other member stays as the code sets it.
    /// </summary>
    public ConfigurableSettingsPicker<TSettings> Configurable(params Expression<Func<TSettings, object?>>[] properties)
    {
        foreach (var property in properties)
        {
            _configurable.Add(GetPropertyName(property));
        }

        return this;
    }

    // s => s.Limit, wrapped in a boxing conversion when the property is a value type
    private static string GetPropertyName(Expression<Func<TSettings, object?>> property)
    {
        var body = property.Body is UnaryExpression { NodeType: ExpressionType.Convert } boxing
            ? boxing.Operand
            : property.Body;

        return body is MemberExpression { Expression: ParameterExpression } member
            ? member.Member.Name
            : throw new ArgumentException(
                $"{property} does not pick a property of {typeof(TSettings).Name}.",
                nameof(property));
    }
}
