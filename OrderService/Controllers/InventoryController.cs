using Grpc.Net.Client;
using InventoryService;
using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IConfiguration _config;

    public InventoryController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("{sku}")]
    public async Task<IActionResult> GetAvailability([FromRoute] string sku)
    {
        var inventoryUrl = _config["InventoryService:Url"];
        if (string.IsNullOrWhiteSpace(inventoryUrl))
        {
            return StatusCode(500, new { error = "Inventory service URL not configured" });
        }

        using var channel = GrpcChannel.ForAddress(inventoryUrl);
        var client = new Inventory.InventoryClient(channel);

        var reply = await client.CheckStockAsync(new CheckStockRequest
        {
            Sku = sku,
            Quantity = 0
        });

        return Ok(new { sku, available = reply.Available, inStock = reply.InStock });
    }
}



