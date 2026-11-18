using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Orders;

// begin-snippet: db-context-factory
internal partial class OrderToolset : Toolset
{
    public OrderToolset() => AddTool(GetOrderAsync);

    /// <summary>
    /// Reads one of the customer's orders by its number.
    /// </summary>
    /// <param name="number">The order's number.</param>
    public static async Task<Order?> GetOrderAsync(int number, IServiceProvider services, CancellationToken ct)
    {
        var contexts = services.GetRequiredService<IDbContextFactory<ShopDb>>();
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Orders.FindAsync([number], ct);
    }
}
// end-snippet
