using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using Prometheus;
using Serilog;

// Allow gRPC over HTTP/2 without TLS (h2c) for internal calls
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        )
    ));
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "OrderService")
    .WriteTo.Console()
    .WriteTo.File("logs/orderservice-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.Seq("http://seq:80")
    .CreateLogger();

builder.Host.UseSerilog();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    var retries = 5;
    while (retries > 0)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch
        {
            retries--;
            if (retries == 0) throw;
            Console.WriteLine(" Waiting for SQL Server... retrying");
            Thread.Sleep(5000);
        }
    }
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.UseHttpMetrics();
app.MapMetrics();
app.Run();
