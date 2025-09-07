using InventoryService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace InventoryService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryDbContext _context;

        public InventoryController(InventoryDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            Log.Information("Retrieving all inventory products");
            try
            {
                var products = await _context.ProductStocks.ToListAsync();
                Log.Information("Successfully retrieved {Count} inventory products", products.Count);
                return Ok(products);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve inventory products");
                return StatusCode(500, new { error = "Failed to retrieve inventory" });
            }
        }
        [HttpGet("{sku}")]
        public async Task<IActionResult> GetBySku(string sku)
        {
            Log.Information("Retrieving inventory for SKU: {Sku}", sku);
            try
            {
                var product = await _context.ProductStocks.FirstOrDefaultAsync(p => p.Sku == sku);
                if (product == null)
                {
                    Log.Warning("Product not found for SKU: {Sku}", sku);
                    return NotFound();
                }
                
                Log.Information("Successfully retrieved inventory for SKU: {Sku}, Available: {Available}", 
                    sku, product.Available);
                return Ok(product);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve inventory for SKU: {Sku}", sku);
                return StatusCode(500, new { error = "Failed to retrieve inventory" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ProductStock product)
        {
            Log.Information("Adding new product to inventory: SKU: {Sku}, Available: {Available}", 
                product.Sku, product.Available);
            try
            {
                _context.ProductStocks.Add(product);
                await _context.SaveChangesAsync();
                
                Log.Information("Successfully added product to inventory: SKU: {Sku}, ID: {Id}", 
                    product.Sku, product.Id);
                return CreatedAtAction(nameof(GetBySku), new { sku = product.Sku }, product);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add product to inventory: SKU: {Sku}", product.Sku);
                return StatusCode(500, new { error = "Failed to add product" });
            }
        }

        [HttpPut("{sku}")]
        public async Task<IActionResult> UpdateStock(string sku, [FromBody] int quantity)
        {
            Log.Information("Updating stock for SKU: {Sku} to quantity: {Quantity}", sku, quantity);
            try
            {
                var product = await _context.ProductStocks.FirstOrDefaultAsync(p => p.Sku == sku);
                if (product == null)
                {
                    Log.Warning("Product not found for stock update: SKU: {Sku}", sku);
                    return NotFound();
                }

                var oldQuantity = product.Available;
                product.Available = quantity;
                await _context.SaveChangesAsync();

                Log.Information("Successfully updated stock for SKU: {Sku}, Old: {OldQuantity}, New: {NewQuantity}", 
                    sku, oldQuantity, quantity);
                return Ok(product);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update stock for SKU: {Sku}", sku);
                return StatusCode(500, new { error = "Failed to update stock" });
            }
        }

        [HttpPost("{sku}/reduce")]
        public async Task<IActionResult> ReduceStock([FromRoute] string sku, [FromBody] ReduceStockRequest request)
        {
            Log.Information("Reducing stock for SKU: {Sku}, Quantity to reduce: {Quantity}", sku, request.Quantity);
            try
            {
                var product = await _context.ProductStocks.FirstOrDefaultAsync(p => p.Sku == sku);
                if (product == null)
                {
                    Log.Warning("Product not found for stock reduction: SKU: {Sku}", sku);
                    return NotFound(new { error = "Product not found", sku });
                }

                if (product.Available < request.Quantity)
                {
                    Log.Warning("Insufficient stock for reduction: SKU: {Sku}, Available: {Available}, Requested: {Quantity}", 
                        sku, product.Available, request.Quantity);
                    return BadRequest(new { error = "Insufficient stock", available = product.Available, requested = request.Quantity });
                }

                var oldQuantity = product.Available;
                product.Available -= request.Quantity;
                await _context.SaveChangesAsync();

                Log.Information("Successfully reduced stock for SKU: {Sku}, Old: {OldQuantity}, Reduced: {Reduced}, New: {NewQuantity}", 
                    sku, oldQuantity, request.Quantity, product.Available);

                return Ok(new { sku, available = product.Available, reduced = request.Quantity });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reduce stock for SKU: {Sku}, Quantity: {Quantity}", sku, request.Quantity);
                return StatusCode(500, new { error = "Failed to reduce stock" });
            }
        }
    }
}

public record ReduceStockRequest(int Quantity);
