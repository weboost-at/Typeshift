using Microsoft.EntityFrameworkCore;

namespace Typeshift.Samples.Orders;

// The app's own database, which the order tools read
internal sealed class ShopDb(DbContextOptions<ShopDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder model) =>
        model.Entity<Order>()
            .HasData(new Order(1, "Shipped"), new Order(2, "Packed"));
}

internal sealed record Order(int Id, string Status);
