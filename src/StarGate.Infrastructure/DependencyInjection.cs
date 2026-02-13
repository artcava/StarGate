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
                var logger = sp.GetRequiredService<ILogger<RabbitMqConnectionFactory>>();
                return RabbitMqConnectionFactory.CreateConnection(options, logger);
            });

            // Register message serializer
            services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();

            // Register RabbitMQ options
            services.AddSingleton(options);

            // Register RabbitMQ broker
            services.AddSingleton<IMessageBroker, RabbitMqBroker>();

            services.AddLogging(builder =>
            {
                builder.Services.AddSingleton(sp =>
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger("RabbitMQ"));
            });
        }
        else
        {
            // Null object pattern - no-op broker when RabbitMQ is disabled
            services.AddSingleton<IMessageBroker, NullMessageBroker>();
        }

        return services;
    }
}
