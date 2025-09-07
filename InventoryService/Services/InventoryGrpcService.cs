using Grpc.Core;
using InventoryService.Data;

namespace InventoryService.Services;

public class InventoryGrpcService : Inventory.InventoryBase
{
    private readonly InventoryDbContext _db;

    public InventoryGrpcService(InventoryDbContext db)
    {
        _db = db;
    }

    public override Task<CheckStockReply> CheckStock(CheckStockRequest request, ServerCallContext context)
    {
        var available = _db.ProductStocks.Where(p => p.Sku == request.Sku).Select(p => p.Available).FirstOrDefault();
        return Task.FromResult(new CheckStockReply
        {
            Available = available,
            InStock = available >= request.Quantity
        });
    }
}
