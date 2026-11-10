using System.Text.Json.Serialization;

namespace Typeshift.Tests.Tools.Support;

[JsonPolymorphic]
[JsonDerivedType(typeof(Circle), "circle")]
[JsonDerivedType(typeof(Square), "square")]
public abstract record Shape;

public sealed record Circle(double Radius) : Shape;

public sealed record Square(double Side) : Shape;

public partial class ShapeToolset : Toolset
{
    public ShapeToolset()
    {
        AddTool(Draw)
            .WithDescription("Draws a shape, with an outline when one is given.");
    }

    /// <param name="shape">The shape to draw.</param>
    /// <param name="outline">The shape of the outline, or null for none.</param>
    public static string Draw(Shape shape, Shape? outline) => $"{shape} {outline}";
}
