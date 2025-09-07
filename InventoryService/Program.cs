using InventoryService.Data;
using InventoryService.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
var builder = WebApplication.CreateBuilder(args);

// Allow both HTTP/1.1 and HTTP/2 so REST and gRPC work together
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2; // REST (HTTP/1.1) and gRPC (HTTP/2)
    });
});

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);
builder.Services.AddControllers();
builder.Services.AddHostedService<InventoryService.Messaging.RabbitMqConsumer>();
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "InventoryService")
    .WriteTo.Console()
    .WriteTo.File("logs/inventoryservice-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.Seq("http://seq:80")
    .CreateLogger();

builder.Host.UseSerilog();
var app = builder.Build();


// Configure the HTTP request pipeline.
// Create schema and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.EnsureCreated();
    // Ensure ProductStocks table exists even if database already has other tables
    db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[ProductStocks]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductStocks](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Sku] NVARCHAR(64) NOT NULL UNIQUE,
        [Available] INT NOT NULL
    );
END
");
    if (!db.ProductStocks.Any())
    {
        db.ProductStocks.AddRange(
            new ProductStock { Sku = "SKU-1", Available = 10 },
            new ProductStock { Sku = "SKU-2", Available = 0 },
            new ProductStock { Sku = "SKU-3", Available = 25 }
        );
        db.SaveChanges();
    }
}

app.MapGrpcService<InventoryGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
app.MapControllers();
app.UseHttpMetrics();
app.MapMetrics();
app.Run();
