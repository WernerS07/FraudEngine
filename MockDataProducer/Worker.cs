using Confluent.Kafka;
using System.Text.Json;
using Shared.Models;
using System.Collections.Generic;



public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    private readonly IProducer<Null, string> _producer = CreateProducer(configuration);
    private readonly Random _random = new();

    private static readonly string[] Locations =
    {
        // Travel Locations (more likely to be flagged)
        "New York", "London", "Paris", "Toronto", "Los Angeles", "Russia", "Japan",
        "South Africa", "Spain", "India", "Egypt", "China", "Australia",

        // Add Local South African cities for more relevant data
        "Cape Town", "Johannesburg", "Durban", "Port Elizabeth", "Pretoria",
        "Bloemfontein", "Nelspruit", "Polokwane", "Kimberley", "East London",
        "Pietermaritzburg", "Mthatha", "Mahikeng", "Welkom", "George",
        "Richards Bay", "Vereeniging", "Springs", "Soweto", "Randburg"
    };

    private static readonly string[] Devices =
    {
        "iPhone 15", "Samsung Galaxy S24", "iPad Pro", "MacBook Pro", "Dell XPS", "Google Pixel 8",
        "OnePlus 11", "Sony Xperia 1", "Huawei P50", "Xiaomi Mi 12","Lenovo ThinkPad X1", "Microsoft Surface Pro"
    };

    public static readonly string[] categories =
    {
        "Groceries", "Electronics", "Clothing", "Travel", "Dining", "Entertainment",
        "Health", "Utilities", "Education", "Automotive"
    };

    // Category weights
    private static readonly int[] CategoryWeights =
    {
        30, // Groceries
        8,  // Electronics
        10, // Clothing
        4,  // Travel
        14, // Dining
        7,  // Entertainment
        8,  // Health
        12, // Utilities
        4,  // Education
        3   // Automotive
    };

    // (min, typicalMax, rareMax) in ZAR
    private static readonly (decimal Min, decimal TypicalMax, decimal RareMax)[] CategoryAmountBands =
    {
        (20m,   1200m,  5000m),   // Groceries
        (80m,   8000m,  60000m),  // Electronics
        (60m,   2500m,  12000m),  // Clothing
        (300m,  15000m, 120000m), // Travel
        (40m,   1800m,  8000m),   // Dining
        (50m,   2500m,  15000m),  // Entertainment
        (60m,   3500m,  25000m),  // Health
        (100m,  4500m,  12000m),  // Utilities
        (200m,  12000m, 80000m),  // Education
        (200m,  25000m, 200000m)  // Automotive
    };

    // Cache serializer options for performance 
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Throughput controls
    private const int MessagesPerLoop = 850; // increase/decrease to tune

    private int PickWeightedCategoryIndex()
    {
        int total = 0;
        for (int i = 0; i < CategoryWeights.Length; i++) total += CategoryWeights[i];

        int roll = _random.Next(1, total + 1);
        int cumulative = 0;
        for (int i = 0; i < CategoryWeights.Length; i++)
        {
            cumulative += CategoryWeights[i];
            if (roll <= cumulative) return i;
        }
        return CategoryWeights.Length - 1;
    }

    private decimal NextAmountZar(int categoryIndex)
    {
        var band = CategoryAmountBands[categoryIndex];

        // 92%: typical spend (right-skewed: many small, fewer large)
        // 7%: higher-but-plausible
        // 1%: rare outlier
        double p = _random.NextDouble();

        decimal max;
        if (p < 0.92) max = band.TypicalMax;
        else if (p < 0.99) max = Math.Min(band.RareMax, band.TypicalMax * 2);
        else max = band.RareMax;

        // Right-skew: square a uniform random to bias towards smaller values
        double u = _random.NextDouble();
        double skew = u * u;

        decimal amount = band.Min + (decimal)skew * (max - band.Min);

        // Round to cents
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private long _producedCount = 0;
    private bool _isRunning = false;
    public bool IsRunning => _isRunning;

    public void SetState(bool state)
    {
        _isRunning = state;
    }
    private static IProducer<Null, string> CreateProducer(IConfiguration configuration)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",

            // Throughput tuning (great for mock traffic)
            CompressionType = CompressionType.Lz4,
            LingerMs = 10,
            BatchNumMessages = 10000,
            Acks = Acks.Leader,

            // Avoid backpressure too early
            QueueBufferingMaxMessages = 1_000_000,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100
        };
        return new ProducerBuilder<Null, string>(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Mock Data Producer Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_isRunning)
                {
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                var sendTasks = new List<Task<DeliveryResult<Null, string>>>(MessagesPerLoop);

                // Produce in batches to remove per-message await overhead
                for (int i = 0; i < MessagesPerLoop && !stoppingToken.IsCancellationRequested; i++)
                {
                    Record transaction = GenerateTransaction();
                    var message = JsonSerializer.Serialize(transaction, JsonOptions);

                    sendTasks.Add(_producer.ProduceAsync(
                        "Transactions-Topic",
                        new Message<Null, string> { Value = message },
                        stoppingToken));

                    _producedCount++;
                }

                try
                {
                    await Task.WhenAll(sendTasks);
                    await Task.Delay(900, stoppingToken);

                }
                catch (ProduceException<Null, string> pex)
                {
                    logger.LogError(pex, "Kafka produce failed: {Reason}", pex.Error.Reason);
                }

                if (_producedCount % 10_000 == 0)
                {
                    logger.LogInformation("Produced {Count} messages", _producedCount);
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error producing message to Kafka");
            }
        }
    }

    private Record GenerateTransaction()
    {
        int categoryIndex = PickWeightedCategoryIndex();
        string category = categories[categoryIndex];

        return new Record
        {
            Amount = NextAmountZar(categoryIndex),
            TimeOfTransaction = DateTime.UtcNow,
            AccountId = _random.Next(10000, 99999),
            ReceipientId = _random.Next(10000, 99999),
            Location = Locations[_random.Next(Locations.Length)],
            Device = Devices[_random.Next(Devices.Length)],
            Category = category
        };
    }

    public override void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignore flush exceptions during shutdown
        }

        _producer?.Dispose();
        base.Dispose();
    }
}
