using System.ComponentModel;

namespace Typeshift.Tests.Tools.Support;

// Tools described as Microsoft.Extensions.AI's samples describe them, one with XML docs beside the attributes
public partial class DescribedToolset : Toolset
{
    public DescribedToolset()
    {
        AddTool(LookUpOrder);

        AddTool(CancelOrder);
    }

    [Description("Looks an order up by its number.")]
    public static string LookUpOrder([Description("The order number, like A-123.")] string number) => number;

    /// <summary>
    /// Cancels an order.
    /// </summary>
    /// <param name="number">The number of the order to cancel.</param>
    [Description("Cancels what the attribute names.")]
    public static string CancelOrder([Description("A number the attribute names.")] string number) => number;
}
