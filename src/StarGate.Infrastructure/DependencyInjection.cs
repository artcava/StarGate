using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using StarGate.Core.Abstractions;
using StarGate.Infrastructure.Messaging;
using StarGate.Infrastructure.Messaging.RabbitMQ;
using Microsoft.Extensions.Logging;

namespace StarGate.Infrastructure;

public static partial class DependencyInjection
{
    public static IServiceCollection AddMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(RabbitMqOptions.SectionName)
            .Get<RabbitMqOptions>();

        if (options?.Enabled == true)
        {
            // Register RabbitMQ connection as singleton
            services.AddSingleton<IConnection>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("RabbitMQ.Connection");
                return RabbitMqConnectionFactory.CreateConnection(options, logger);
            });

            // Register message serializer
            services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

            // Register RabbitMQ options
            services.AddSingleton(options);

            // Register RabbitMQ broker and consumer
            services.AddSingleton<IMessageBroker, RabbitMqBroker>();
            services.AddSingleton<IMessageConsumer, RabbitMqConsumer>();
        }
        else
        {
            // Null object pattern - no-op broker and consumer when RabbitMQ is disabled
            services.AddSingleton<IMessageBroker, NullMessageBroker>();
            services.AddSingleton<IMessageConsumer, NullMessageConsumer>();
        }

        return services;
    }
}
