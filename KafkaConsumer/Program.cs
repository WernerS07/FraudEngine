using KafkaConsumer.Services;
using Shared.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared.Services;
using StackExchange.Redis;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Error()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Error)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Error) 
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog();

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

    builder.Services.AddDbContext<TransactionDBContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseNpgsql(connectionString);
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    });

    // Add FraudCheck as Scoped
    builder.Services.AddScoped<FraudCheck>();
    
    // Add Workers
    builder.Services.AddHostedService<TransactionConsumer>();

    
    var host = builder.Build();

   
    Log.Information("Kafka Consumer starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Kafka Consumer terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}