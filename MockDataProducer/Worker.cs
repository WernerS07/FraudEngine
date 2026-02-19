using Confluent.Kafka;
using System.Text.Json;

using Shared.Models;
using System.Runtime;
using Microsoft.EntityFrameworkCore.Diagnostics;



public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    private readonly IProducer<Null, string> _producer = CreateProducer(configuration);
    private readonly Random _random = new();
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
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092"
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
                if(_isRunning){
                    Record transaction = GenerateTransaction();
                    var message = JsonSerializer.Serialize(transaction);

                    await _producer.ProduceAsync("Transactions-Topic", new Message<Null, string> 
                    { 
                        Value = message 
                    }, stoppingToken);

                    logger.LogInformation("Produced transaction for User: {AccountId}, Amount: {Amount}",
                        transaction.AccountId, transaction.Amount);

                    //await Task.Delay(1000, stoppingToken); // Send every 1000ms
                }
                else
                {
                    await Task.Delay(1000,stoppingToken);
                    continue;
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
        var locations = new[] { "New York", "London", "Paris", "Toronto", "Los Angeles", "Russia", "Japan", "South Africa", "Spain", "India", "Egypt", "China", "Australia" };
        var devices = new[] { "iPhone 15", "Samsung Galaxy S24", "iPad Pro", "MacBook Pro", "Dell XPS", "Google Pixel 8" };

        return new Record
        {
            Amount = Math.Round((decimal)(_random.NextDouble() * 15000) / _random.Next(1,10), 2),
            TimeOfTransaction = DateTime.UtcNow,
            AccountId = _random.Next(1000, 4000),
            ReceipientId = _random.Next(1000, 4000),
            Location = locations[_random.Next(locations.Length)],
            Device = devices[_random.Next(devices.Length)]
        };
    }

    public override void Dispose()
    {
        _producer?.Dispose();
        base.Dispose();
    }
}
