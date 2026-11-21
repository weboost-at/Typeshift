using System.Reflection;
using Typeshift.Generators.Models;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

// The pipeline caches a step whose model equals the one of the run before. A member that compares by reference, a
// symbol, a syntax node or a plain array for example, makes every edit a change, and only for the code that fills it
public class ModelShapeTests
{
    private static readonly string? Models = typeof(ToolsetModel).Namespace;

    [Fact]
    public void Every_Model_Holds_Only_Equatable_Values()
    {
        // Arrange
        var models = typeof(ToolsetModel).Assembly.GetTypes().Where(IsModel).ToList();

        // Act
        var violations = models
            .Where(model => !IsRecord(model))
            .Select(model => $"{model.Name} is no record: declare it as one, so that it compares by its values")
            .Concat(models.SelectMany(model => model
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !IsEquatable(field.FieldType))
                .Select(field => $"{model.Name}.{NameOf(field)} is a {field.FieldType}: hold a string, a bool, an "
                    + "int, an enum, another model or an EquatableArray<T> of those")))
            .ToList();

        // Assert
        violations.Should().BeEmpty(Listing.Of(violations));
    }

    private static bool IsModel(Type type) =>
        type.Namespace == Models && type is { IsClass: true, IsAbstract: false, IsNested: false };

    private static bool IsEquatable(Type type) =>
        type == typeof(string)
        || type == typeof(bool)
        || type == typeof(int)
        || type.IsEnum
        || (IsModel(type) && IsRecord(type))
        || (type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(EquatableArray<>)
            && IsEquatable(type.GenericTypeArguments[0]));

    // The method the compiler writes for a record, and for no other type
    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$") is not null;

    // The member a field the compiler wrote stands behind
    private static string NameOf(FieldInfo field) =>
        field.Name.StartsWith('<') ? field.Name[1..field.Name.IndexOf('>')] : field.Name;
}
