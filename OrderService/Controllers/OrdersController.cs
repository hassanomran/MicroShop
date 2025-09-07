using Grpc.Net.Client;
using InventoryService;
using Microsoft.AspNetCore.Mvc;
using OrderService.Data;
using OrderService.Messaging;
using OrderService.Models;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using Serilog;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly RabbitMqPublisher _publisher;
    private readonly OrderDbContext _dbContext;

    // Polly policies
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreaker;

    public OrdersController(IConfiguration config, OrderDbContext dbContext)
    {
        _config = config;
        _publisher = new RabbitMqPublisher("rabbitmq");
        _dbContext = dbContext;

        // 🔹 Polly Retry Policy (3 retries with exponential backoff)
        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine($"⚠️ Retry {retryAttempt} after {timespan.TotalSeconds}s due to {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                });

        // 🔹 Polly Circuit Breaker Policy
        _circuitBreaker = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 2,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    Console.WriteLine($"🚨 Circuit broken for {breakDelay.TotalSeconds}s due to {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                },
                onReset: () => Console.WriteLine("✅ Circuit closed, calls to InventoryService restored."),
                onHalfOpen: () => Console.WriteLine("⚡ Circuit in half-open state, testing InventoryService...")
            );
    }

    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] OrderRequest request)
    {
        Log.Information("Order placement request received for SKU: {Sku}, Quantity: {Quantity}", 
            request.Sku, request.Quantity);

        var inventoryBaseUrl = _config["InventoryService:Url"];
        if (string.IsNullOrWhiteSpace(inventoryBaseUrl))
        {
            Log.Error("Inventory service URL not configured");
            return StatusCode(500, new { error = "Inventory service URL not configured" });
        }

        using var http = new HttpClient();
        HttpResponseMessage restResponse;

        try
        {
            Log.Information("Checking inventory for SKU: {Sku}", request.Sku);
            // 🔹 Execute HTTP request with Retry + CircuitBreaker
            restResponse = await _retryPolicy
                .WrapAsync(_circuitBreaker)
                .ExecuteAsync(() => http.GetAsync($"{inventoryBaseUrl}/api/inventory/{request.Sku}"));
        }
        catch (BrokenCircuitException)
        {
            Log.Warning("Circuit breaker is open for InventoryService, rejecting order for SKU: {Sku}", request.Sku);
            return StatusCode(503, new { error = "Inventory service temporarily unavailable (circuit open)" });
        }

        if (!restResponse.IsSuccessStatusCode)
        {
            Log.Error("Failed to check inventory for SKU: {Sku}, Status: {StatusCode}", 
                request.Sku, restResponse.StatusCode);
            return StatusCode(502, new { error = "Inventory service unavailable", status = (int)restResponse.StatusCode });
        }

        using var stream = await restResponse.Content.ReadAsStreamAsync();
        var payload = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
        var available = payload.TryGetProperty("Available", out var av)
            ? av.GetInt32()
            : payload.TryGetProperty("available", out var av2) ? av2.GetInt32() : 0;
        var inStock = available >= request.Quantity;

        Log.Information("Inventory check completed for SKU: {Sku}, Available: {Available}, Requested: {Quantity}, InStock: {InStock}", 
            request.Sku, available, request.Quantity, inStock);

        if (inStock)
        {
            Log.Information("Stock available, proceeding to reduce inventory for SKU: {Sku}, Quantity: {Quantity}", 
                request.Sku, request.Quantity);
            
            // Reduce inventory first
            HttpResponseMessage reduceResponse;
            try
            {
                reduceResponse = await _retryPolicy
                    .WrapAsync(_circuitBreaker)
                    .ExecuteAsync(() => http.PostAsJsonAsync($"{inventoryBaseUrl}/api/inventory/{request.Sku}/reduce", 
                        new { Quantity = request.Quantity }));
            }
            catch (BrokenCircuitException)
            {
                Log.Warning("Circuit breaker is open during inventory reduction for SKU: {Sku}", request.Sku);
                return StatusCode(503, new { error = "Inventory service temporarily unavailable (circuit open)" });
            }

            if (!reduceResponse.IsSuccessStatusCode)
            {
                Log.Error("Failed to reduce inventory for SKU: {Sku}, Status: {StatusCode}", 
                    request.Sku, reduceResponse.StatusCode);
                return StatusCode(502, new { error = "Failed to reduce inventory", status = (int)reduceResponse.StatusCode });
            }

            // Persist order
            var productId = TryParseProductIdFromSku(request.Sku);
            var order = new Order
            {
                ProductId = productId,
                Quantity = request.Quantity
            };

            try
            {
                _dbContext.Orders.Add(order);
                await _dbContext.SaveChangesAsync();
                
                Log.Information("Order successfully created with ID: {OrderId} for SKU: {Sku}, Quantity: {Quantity}", 
                    order.Id, request.Sku, request.Quantity);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save order to database for SKU: {Sku}, Quantity: {Quantity}", 
                    request.Sku, request.Quantity);
                return StatusCode(500, new { error = "Failed to save order" });
            }

            var message = $"Order placed: Id={order.Id}, SKU={request.Sku}, Qty={request.Quantity}";
            try
            {
                _publisher.Publish("order_created", message);
                Log.Information("Order created event published to RabbitMQ for OrderId: {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to publish order created event for OrderId: {OrderId}", order.Id);
                // Don't fail the order if messaging fails
            }

            Log.Information("Order placement completed successfully for SKU: {Sku}, OrderId: {OrderId}, Remaining Stock: {RemainingStock}", 
                request.Sku, order.Id, available - request.Quantity);

            return Ok(new { status = "Order confirmed", available = available - request.Quantity, orderId = order.Id });
        }
        else
        {
            Log.Warning("Order rejected due to insufficient stock for SKU: {Sku}, Available: {Available}, Requested: {Quantity}", 
                request.Sku, available, request.Quantity);
            return BadRequest(new { status = "Out of stock", available = available });
        }
    }

    private static int TryParseProductIdFromSku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return 0;
        var digits = new string(sku.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var id) ? id : 0;
    }
}

public record OrderRequest(string Sku, int Quantity);
