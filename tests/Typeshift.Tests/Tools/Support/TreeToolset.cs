namespace Typeshift.Tests.Tools.Support;

// Refers to itself, so its schema carries $refs
public record Category(string Name, Category[] Children);

public partial class TreeToolset : Toolset
{
    public TreeToolset()
    {
        AddTool(CountCategories)
            .WithDescription("Counts the categories of a tree.");
    }

    public static int CountCategories(Category tree) => 1 + tree.Children.Sum(CountCategories);
}
