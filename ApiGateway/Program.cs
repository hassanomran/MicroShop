using Prometheus;
using Serilog;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add YARP (Yet Another Reverse Proxy)
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ApiGateway")
    .WriteTo.Console()
    .WriteTo.File("logs/apigateway-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.Seq("http://seq:80")
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add request/response logging middleware
app.Use(async (context, next) =>
{
    var startTime = DateTime.UtcNow;
    var requestId = Guid.NewGuid().ToString("N")[..8];
    
    Log.Information("Request started: {RequestId} {Method} {Path} from {RemoteIp}", 
        requestId, context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
    
    context.Items["RequestId"] = requestId;
    context.Items["StartTime"] = startTime;
    
    await next();
    
    var duration = DateTime.UtcNow - startTime;
    Log.Information("Request completed: {RequestId} {Method} {Path} {StatusCode} in {Duration}ms", 
        requestId, context.Request.Method, context.Request.Path, context.Response.StatusCode, duration.TotalMilliseconds);
});

app.UseAuthorization();

app.MapControllers();
app.MapReverseProxy();
app.UseHttpMetrics();   
app.MapMetrics();       

app.Run();