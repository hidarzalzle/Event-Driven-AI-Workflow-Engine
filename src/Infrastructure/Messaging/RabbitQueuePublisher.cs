using System.Text;
using Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public sealed class RabbitQueuePublisher(IConfiguration config, ILogger<RabbitQueuePublisher> logger) : IQueuePublisher
{
    public Task PublishAsync(string topic, string routingKey, string payload, CancellationToken ct)
    {
        if (!config.GetValue("RabbitMQ:Enabled", true))
        {
            logger.LogInformation("RabbitMQ disabled. Topic {Topic}, RoutingKey {RoutingKey}", topic, routingKey);
            return Task.CompletedTask;
        }

        var factory = new ConnectionFactory { Uri = new Uri(config["RabbitMQ:Connection"]!) };
        using var conn = factory.CreateConnection();
        using var channel = conn.CreateModel();
        channel.ExchangeDeclare(topic, ExchangeType.Topic, durable: true);
        channel.BasicPublish(topic, routingKey, body: Encoding.UTF8.GetBytes(payload));
        return Task.CompletedTask;
    }
}
