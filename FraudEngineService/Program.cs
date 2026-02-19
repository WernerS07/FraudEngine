using Microsoft.EntityFrameworkCore;
using Shared.Models;
using Shared.Services;
using FraudEngineService.Services;
using StackExchange.Redis;


var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

// Add Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var redisConnection = configuration.GetConnectionString("Redis");
    return ConnectionMultiplexer.Connect(redisConnection ?? "localhost:6379");
});

// Add Memory Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Records";
});

builder.Services.AddSingleton<RedisCacheService>();

// Add DbContext
builder.Services.AddDbContext<TransactionDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<RecordService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapControllers(); 

app.Run();