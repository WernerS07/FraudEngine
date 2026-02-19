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

builder.Services.AddDbContext<TransactionDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("Shared")));

builder.Services.AddScoped<RecordService>();

var app = builder.Build();

 // Run migrations on startup
    using (var scope = app.Services.CreateScope())
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
// Configure the HTTP request pipeline
app.MapControllers(); 

app.Run();