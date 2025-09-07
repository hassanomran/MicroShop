using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<ProductStock> ProductStocks { get; set; }
}

public class ProductStock
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Available { get; set; }
}



