using System.Text;
using RabbitMQ.Client;

namespace OrderService.Messaging
{
    public class RabbitMqPublisher
    {
        private readonly ConnectionFactory _factory;

        public RabbitMqPublisher(string hostName = "rabbitmq")
        {
            _factory = new ConnectionFactory() { HostName = hostName };
        }

        public void Publish(string queueName, string message)
        {
            using var connection = _factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: queueName,
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(exchange: "",
                                 routingKey: queueName,
                                 basicProperties: null,
                                 body: body);
        }
    }
}
