using MassTransit;
using NotificationService.Consumers;

var builder = Host.CreateApplicationBuilder(args);

// Worker-сервис: никакого HTTP, только подписка на очередь RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        // MassTransit сам создаёт очередь и привязывает Consumer
        cfg.ConfigureEndpoints(ctx);
    });
});

var host = builder.Build();
host.Run();
