using System.Text.Json;
using Confluent.Kafka;
using Shared.Models;
using Microsoft.EntityFrameworkCore;
using KafkaConsumer.Models;
using System.Net;
namespace KafkaConsumer.Services;

public class TransactionConsumer : BackgroundService
{
    private readonly ILogger<TransactionConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    
    public TransactionConsumer(
        ILogger<TransactionConsumer> logger, 
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transaction Consumer starting at: {time}", DateTimeOffset.Now);

        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = _configuration["Kafka:GroupId"] ?? "Transactions-Consumers",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SessionTimeoutMs = 10000,
            MaxPollIntervalMs = 300000
        };

        var topic = _configuration["Kafka:Topic"] ?? "Transactions-Topic";

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka Error: {Reason}", e.Reason))
            .Build();

        try
        {
            consumer.Subscribe(topic);
            _logger.LogInformation("Subscribed to Kafka topic: {Topic}", topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    
                    if (consumeResult?.Message?.Value != null)
                    {
                        _logger.LogDebug("Received message from partition {Partition}, offset {Offset}",
                            consumeResult.Partition.Value, consumeResult.Offset.Value);
                            
                        await ProcessMessageAsync(consumeResult.Message.Value, stoppingToken);
                        
                        consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message: {Error}", ex.Error.Reason);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transaction Consumer stopping.");
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("Transaction Consumer stopped.");
        }
    }

    private async Task ProcessMessageAsync(string messageValue, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = JsonSerializer.Deserialize<Record>(messageValue, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (transaction == null)
            {
                _logger.LogWarning("Failed to deserialize message: {Message}", messageValue);
                return;
            }

            _logger.LogDebug("Processing transaction for account {AccountId}, amount ${Amount}",
                transaction.AccountId, transaction.Amount);

            var record = new Record
            {
                Amount = transaction.Amount,
                TimeOfTransaction = transaction.TimeOfTransaction,
                AccountId = transaction.AccountId,
                ReceipientId = transaction.ReceipientId,
                Location = transaction.Location,
                Device = transaction.Device,
                Isfraud = false
            };

            // Use scoped services
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TransactionDBContext>();
            var fraudCheck = scope.ServiceProvider.GetRequiredService<FraudCheck>();

            // Run fraud detection
            FraudCheckResponse fraudResponse = await fraudCheck.CheckFraudAsync(record);
            
            // Apply fraud check results
            record.Isfraud = fraudResponse.IsFraud;
            record.FraudReason = fraudResponse.Reason;

            // Save to database
            await dbContext.Records.AddAsync(record, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "✓ Transaction saved: ID={TransactionId}, Account={AccountId}, Amount=${Amount}, Fraud={IsFraud}, Reason={Reason}",
                record.TransactionId, record.AccountId, record.Amount, record.Isfraud, record.FraudReason);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed for message: {Message}", messageValue);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database save failed");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message");
            throw;
        }
    }
}