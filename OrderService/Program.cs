using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("OrderDb");

    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
});

// MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentProcessedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]);
            h.Password(builder.Configuration["RabbitMQ:Password"]);
        });
        cfg.ReceiveEndpoint("payment-processed", e =>
        {
            e.ConfigureConsumer<PaymentProcessedConsumer>(context);
        });
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddHostedService<OutboxProcessorMassTransit>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderContext>();
    var maxRetries = 10;
    var retryCount = 0;

    while (retryCount < maxRetries)
    {
        try
        {
            Console.WriteLine($"OrderService migration attempt {retryCount + 1}...");

            dbContext.Database.Migrate();
            Console.WriteLine("OrderService database migration completed successfully.");

            EnsureOrderTablesCreated(dbContext);

            break;
        }
        catch (Exception ex)
        {
            retryCount++;
            Console.WriteLine($"OrderService migration attempt {retryCount} failed: {ex.Message}");

            if (retryCount >= maxRetries)
            {
                Console.WriteLine("Max retries reached. Migration failed.");
                throw;
            }

            await Task.Delay(5000);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();

void EnsureOrderTablesCreated(OrderContext context)
{
    try
    {
        var ordersTableSql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES 
                           WHERE TABLE_NAME = 'Orders')
            BEGIN
                CREATE TABLE Orders (
                    Id UNIQUEIDENTIFIER PRIMARY KEY,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    Status INT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NULL
                );
                CREATE INDEX IX_Orders_UserId ON Orders(UserId);
                PRINT 'Orders table created in OrderDb.';
            END";

        context.Database.ExecuteSqlRaw(ordersTableSql);
        Console.WriteLine("Orders table verified/created.");

        var outboxTableSql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES 
                           WHERE TABLE_NAME = 'OutboxMessages')
            BEGIN
                CREATE TABLE OutboxMessages (
                    Id UNIQUEIDENTIFIER PRIMARY KEY,
                    EventType NVARCHAR(255) NOT NULL,
                    EventData NVARCHAR(MAX) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    ProcessedAt DATETIME2 NULL
                );
                CREATE INDEX IX_OutboxMessages_ProcessedAt ON OutboxMessages(ProcessedAt);
                PRINT 'OutboxMessages table created in OrderDb.';
            END";

        context.Database.ExecuteSqlRaw(outboxTableSql);
        Console.WriteLine("OutboxMessages table verified/created.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error ensuring OrderService tables: {ex.Message}");
    }
}