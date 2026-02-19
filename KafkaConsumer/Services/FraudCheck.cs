using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using Shared.Models;
using Shared.Services;
using KafkaConsumer.Models;

namespace KafkaConsumer.Services;

public class FraudCheck 
{
    private readonly ILogger<FraudCheck> _logger;
    private readonly TransactionDBContext _context;
    private readonly RedisCacheService _cache;
    
    private static readonly TimeSpan FlaggedDataCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecentTransactionsCacheDuration = TimeSpan.FromMinutes(2);

    public FraudCheck(
        ILogger<FraudCheck> logger,
        TransactionDBContext context,
        RedisCacheService cache)
    {
        _logger = logger;
        _context = context;
        _cache = cache;
    }

    public async Task<FraudCheckResponse> CheckFraudAsync(Record record)
    {
        try
        {
            var fraudReasons = new List<string>();
            
            // Run checks with reason tracking
            if (IsHighAmount(record.Amount))
                fraudReasons.Add("Rule 1: High Amount");
                
            if (await IsFlaggedLocationAsync(record.Location))
                fraudReasons.Add("Rule 2: Flagged Location");
                
            if (await IsFlaggedDeviceAsync(record.Device))
                fraudReasons.Add($"Rule 3: Flagged Device ({record.Device})");
                
            if (await IsMultipleTransactionsAsync(record.AccountId, record.TimeOfTransaction))
                fraudReasons.Add("Rule 6: Multiple Rapid Transactions");
                
            if (await IsFlaggedAccountAsync(record.ReceipientId, record.AccountId))
                fraudReasons.Add($"Rule 4: Flagged Account ID({record.AccountId}) or Recipient ID({record.ReceipientId})");
                
            if (IsUnusualTime(record.TimeOfTransaction))
                fraudReasons.Add("Rule 5: Unusual Time");

            var isFraud = fraudReasons.Any();
            record.Isfraud = isFraud;

            if (isFraud)
            {
                _logger.LogWarning(
                    "FRAUD DETECTED: Amount={Amount}, Account={AccountId}, Recipient={RecipientId}, Location={Location}, Time={Time}, Reasons=[{Reasons}]",
                    record.Amount, record.AccountId, record.ReceipientId, record.Location, record.TimeOfTransaction, 
                    string.Join(", ", fraudReasons));
            }
            else
            {
                _logger.LogInformation(
                    "Clean Transaction: Amount={Amount}, Account={AccountId}, Recipient={RecipientId}, Location={Location}",
                    record.Amount, record.AccountId, record.ReceipientId, record.Location);
            }
            var Response = new FraudCheckResponse
            {
                IsFraud = isFraud,
                Reason = string.Join(", ", fraudReasons)
            };
            return Response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fraud check for account {AccountId}", record.AccountId);
            record.Isfraud = false;

            var Response = new FraudCheckResponse
            {
                IsFraud = record.Isfraud,
                Reason = string.Join(", ", "Fraud Check Failed")
            };
            return Response;
        }
    }

    // RULE 1: IF transaction amount is above limit -> flag transaction
    private static bool IsHighAmount(decimal amount)
    {
        return amount > 10000;
    }

    // RULE 2: If transaction was made in flagged location -> flag transaction
    private async Task<bool> IsFlaggedLocationAsync(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return false; 

        const string cacheKey = "FlaggedLocations";

        var flaggedLocations = await GetOrSetCacheAsync(
            cacheKey,
            async () => await _context.FlaggedLocations
                .Select(r => r.Location.ToLower())
                .Distinct()
                .ToListAsync()
        );

        // Check cache first
        if (flaggedLocations.Contains(location.ToLower()))
            return true;

        // Cache miss - check DB directly for this specific location
        var existsInDb = await _context.FlaggedLocations
            .AnyAsync(r => r.Location.ToLower() == location.ToLower());
        
        if (existsInDb)
        {
            // Found in DB but not in cache - add it and update cache
            flaggedLocations.Add(location.ToLower());
            await _cache.SetCacheAsync(cacheKey, flaggedLocations, FlaggedDataCacheDuration);
            _logger.LogInformation("Added newly flagged location to cache: {Location}", location);
            return true;
        }

        return false;
    }

    // RULE 3: Check for flagged devices
    private async Task<bool> IsFlaggedDeviceAsync(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return false; 

        const string cacheKey = "FlaggedDevices";

        var flaggedDevices = await GetOrSetCacheAsync(
            cacheKey,
            async () => await _context.FlaggedDevices
                .Select(r => r.DeviceName.ToLower())
                .Distinct()
                .ToListAsync()
        );

        if (flaggedDevices.Contains(deviceName.ToLower()))
            return true;

        // Cache miss - check DB directly
        var existsInDb = await _context.FlaggedDevices
            .AnyAsync(r => r.DeviceName.ToLower() == deviceName.ToLower());
        
        if (existsInDb)
        {
            flaggedDevices.Add(deviceName.ToLower());
            await _cache.SetCacheAsync(cacheKey, flaggedDevices, FlaggedDataCacheDuration);
            _logger.LogInformation("Added newly flagged device to cache: {Device}", deviceName);
            return true;
        }

        return false;
    }

    // RULE 4
    private async Task<bool> IsFlaggedAccountAsync(int recipientId,int accountId)
    {
        const string cacheKey = "FlaggedAccounts";

        var flaggedAccounts = await GetOrSetCacheAsync(
            cacheKey,
            async () => await _context.FlaggedAccounts
                .Select(r => r.AccountId)
                .Distinct()
                .ToListAsync()
        );

        if (flaggedAccounts.Contains(recipientId) ||flaggedAccounts.Contains(accountId) )
            return true;

        // Cache miss - check DB for ONLY these 2 specific IDs (efficient!)
        var flaggedIds = await _context.FlaggedAccounts
            .Where(r => r.AccountId == recipientId || r.AccountId == accountId)
            .Select(r => r.AccountId)
            .ToListAsync();

        bool temp =false;
        if (flaggedIds.Contains(recipientId))
        {
            temp = true;
            flaggedAccounts.Add(recipientId);
            await _cache.SetCacheAsync(cacheKey, flaggedAccounts, FlaggedDataCacheDuration);
            _logger.LogInformation("Added newly flagged recipient to cache: {recipientId}", recipientId);
        }
        if(flaggedIds.Contains(accountId))
        {
            temp = true;
            flaggedAccounts.Add(accountId);
            await _cache.SetCacheAsync(cacheKey, flaggedAccounts, FlaggedDataCacheDuration);
            _logger.LogInformation("Added newly flagged accountId to cache: {accountId}", accountId);
        }
        return temp;
}

    // RULE 5: Transaction Occuring at an unusual time
    // Between 12am and 5am
    private static bool IsUnusualTime(DateTime timeOfTransaction)
    {
        int hour = timeOfTransaction.Hour;
        if( (hour >= 2) && (hour < 5))
            return true;        

        return false;
    }

    // Rule 6: Multiple Transactions Within 2 minutes
    private async Task<bool> IsMultipleTransactionsAsync(int accountId, DateTime transactionTime)
    {
        string cacheKey = $"recent_transactions_{accountId}";
        
        // Calculate time window ONCE at the start
        var twoMinutesAgo = DateTime.UtcNow.AddMinutes(-2);
        
        // Get or fetch recent transactions for this account
        var recentTransactions = await GetOrSetCacheAsync(
            cacheKey,
            async () =>
            {
                return await _context.Records
                    .Where(r => r.AccountId == accountId && r.TimeOfTransaction >= twoMinutesAgo)
                    .Select(r => r.TimeOfTransaction)
                    .OrderByDescending(t => t)
                    .ToListAsync();
            },
            RecentTransactionsCacheDuration // 2 minutes
        );

        // No need to re-filter - the data is already filtered by the DB query
        // Just add current transaction
        recentTransactions.Add(transactionTime);

        // Update cache with new transaction added
        await _cache.SetCacheAsync(cacheKey, recentTransactions, RecentTransactionsCacheDuration);

        // Check if there are more than 3 transactions in the last 2 minutes
        bool isFraud = recentTransactions.Count > 3;

        if (isFraud)
        {
            _logger.LogWarning(
                "Multiple rapid transactions detected: Account={AccountId}, Count={Count} in last 2 minutes",
                accountId, recentTransactions.Count);
        }

        return isFraud;
    }
   

    // HOW TO USE: 
    // PASS in the cache key, and a async DB get function
    // Returns a list of type T 
    // If cacheKey exists, return cache hit, if cache miss, get from db and update cache
    public async Task<List<T>> GetOrSetCacheAsync<T>(
        string cacheKey,
        Func<Task<List<T>>> fetchFromDataBase,
        TimeSpan? cacheDuration = null)
    {   
        // Check if data exists in cache
        var cachedProduct = await _cache.GetCacheAsync<List<T>>(cacheKey);
        if (cachedProduct != null)
        {
           _logger.LogInformation("Cache Hit for {cacheKey}", cacheKey);
           return cachedProduct;
        }
        _logger.LogInformation("Cache Miss for {cacheKey}, fetching from DB", cacheKey);

        List<T> data = await fetchFromDataBase();
        if(data != null )
        {
            var duration = cacheDuration ?? FlaggedDataCacheDuration;
            await _cache.SetCacheAsync(cacheKey, data, duration);
        }

        return data ?? [];
    }
    
}
