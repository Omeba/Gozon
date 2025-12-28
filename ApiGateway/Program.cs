using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("OrderService", client =>
{
    client.BaseAddress = new Uri("http://order-service/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("PaymentService", client =>
{
    client.BaseAddress = new Uri("http://payment-service/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();