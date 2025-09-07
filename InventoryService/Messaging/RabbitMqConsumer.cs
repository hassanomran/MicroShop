using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InventoryService.Messaging
{
    public class RabbitMqConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMqConsumer> _logger;
        public RabbitMqConsumer(IServiceProvider serviceProvider, ILogger<RabbitMqConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            var factory = new ConnectionFactory() { HostName = "rabbitmq" }; // from docker-compose
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: "order_created",
                                  durable: false,
                                  exclusive: false,
                                  autoDelete: false,
                                  arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received +=async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($"📩 Received message: {message}");

                // TODO: update SQL Server inventory here
                try
                {
                    // message format: "SKU=ABC123,Qty=2"
                    var parts = message.Split(',');
                    var sku = parts[0].Split('=')[1];
                    var qty = int.Parse(parts[1].Split('=')[1]);

                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                    var product = await db.ProductStocks.FirstOrDefaultAsync(p => p.Sku == sku);
                    if (product != null && product.Available >= qty)
                    {
                        product.Available -= qty;
                        await db.SaveChangesAsync();
                        _logger.LogInformation($"✅ Stock updated: {sku}, Remaining={product.Available}");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ Not enough stock for {sku}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                }
            };

            _channel.BasicConsume(queue: "order_created",
                                 autoAck: true,
                                 consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
