using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("PaymentDb");

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
    x.AddConsumer<OrderCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]);
            h.Password(builder.Configuration["RabbitMQ:Password"]);
        });
        cfg.ReceiveEndpoint("order-created", e =>
        {
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
        });
        cfg.ConfigureEndpoints(context);
    });
});

// Регистрируем PaymentOutboxProcessor как HostedService
builder.Services.AddHostedService<PaymentOutboxProcessor>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentContext>();
    var maxRetries = 10;
    var retryCount = 0;

    while (retryCount < maxRetries)
    {
        try
        {
            Console.WriteLine($"PaymentService migration attempt {retryCount + 1}...");

            // Применяем миграции
            dbContext.Database.Migrate();
            Console.WriteLine("PaymentService database migration completed successfully.");

            // Создаем таблицы, если они не созданы миграцией
            EnsurePaymentTablesCreated(dbContext);

            break;
        }
        catch (Exception ex)
        {
            retryCount++;
            Console.WriteLine($"PaymentService migration attempt {retryCount} failed: {ex.Message}");

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

void EnsurePaymentTablesCreated(PaymentContext context)
{
    try
    {
        // Проверяем и создаем таблицу Accounts
        var accountsTableSql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES 
                           WHERE TABLE_NAME = 'Accounts')
            BEGIN
                CREATE TABLE Accounts (
                    Id UNIQUEIDENTIFIER PRIMARY KEY,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    Balance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NULL
                );
                CREATE UNIQUE INDEX IX_Accounts_UserId ON Accounts(UserId);
                PRINT 'Accounts table created in PaymentDb.';
            END";

        context.Database.ExecuteSqlRaw(accountsTableSql);
        Console.WriteLine("Accounts table verified/created.");

        // Проверяем и создаем таблицу OutboxMessages
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
                PRINT 'OutboxMessages table created in PaymentDb.';
            END";

        context.Database.ExecuteSqlRaw(outboxTableSql);
        Console.WriteLine("OutboxMessages table verified/created.");

        // Проверяем и создаем таблицу InboxMessages
        var inboxTableSql = @"
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES 
                   WHERE TABLE_NAME = 'InboxMessages')
    BEGIN
        CREATE TABLE InboxMessages (
            Id UNIQUEIDENTIFIER PRIMARY KEY,
            MessageId NVARCHAR(255) NOT NULL,
            EventType NVARCHAR(255) NOT NULL,
            EventData NVARCHAR(MAX) NOT NULL,
            ProcessedAt DATETIME2 NOT NULL,
            CreatedAt DATETIME2 NOT NULL
        );
        CREATE UNIQUE INDEX IX_InboxMessages_MessageId ON InboxMessages(MessageId);
        PRINT 'InboxMessages table created in PaymentDb.';
    END";

        context.Database.ExecuteSqlRaw(inboxTableSql);
        Console.WriteLine("InboxMessages table verified/created.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error ensuring PaymentService tables: {ex.Message}");
    }
}