using KafkaConsumer.Services;
using Shared.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared.Services;
using StackExchange.Redis;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning) 
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

    // Run migrations on startup
    using (var scope = host.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<TransactionDBContext>();
            context.Database.Migrate();
            Console.WriteLine("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while migrating the database.");
        }
    }  
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