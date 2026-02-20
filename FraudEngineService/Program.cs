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

            Console.WriteLine("Seeding database with sample flagged locations, accounts and devices...");

            // Flagged Locations 
            var flaggedLocations = new List<FlaggedLocation>
            {
                new FlaggedLocation { Location = "New York" },
                new FlaggedLocation { Location = "China" },
                new FlaggedLocation { Location = "India" },
                new FlaggedLocation { Location = "Russia" },
            };
            context.FlaggedLocations.AddRange(flaggedLocations);
            
            // Flagged Devices
            var flaggedDevices = new List<FlaggedDevice>
            {
                new FlaggedDevice { DeviceName = "Xiaomi Mi 12" },
                new FlaggedDevice { DeviceName = "Google Pixel 8" },
                new FlaggedDevice { DeviceName = "OnePlus 11" },
            };
            context.FlaggedDevices.AddRange(flaggedDevices);

            // Flagged Accounts
            var flaggedAccounts = new List<FlaggedAccount>
            {
                new FlaggedAccount { AccountId = 33010 },
                new FlaggedAccount { AccountId = 44020 },
                new FlaggedAccount { AccountId = 55030 },
                new FlaggedAccount { AccountId = 66040 },
                new FlaggedAccount { AccountId = 77050 },
                new FlaggedAccount { AccountId = 14020 },
                new FlaggedAccount { AccountId = 25030 },
                new FlaggedAccount { AccountId = 96040 },
                new FlaggedAccount { AccountId = 37050 },
                new FlaggedAccount { AccountId = 74020 },
                new FlaggedAccount { AccountId = 65030 },
                new FlaggedAccount { AccountId = 90040 },
                new FlaggedAccount { AccountId = 57050 }
            };
            context.FlaggedAccounts.AddRange(flaggedAccounts);

            context.SaveChanges();
            Console.WriteLine("Database seeding completed.");
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