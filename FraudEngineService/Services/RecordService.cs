using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Shared.Models;
using Shared.Services;
using FraudEngineService.Models;
using System.Reflection.Metadata.Ecma335;

namespace FraudEngineService.Services;
public class RecordService 
{
    private static readonly TimeSpan FlaggedDataCacheDuration = TimeSpan.FromMinutes(5);

    private readonly ILogger<RecordService> _logger;
    private readonly TransactionDBContext _context;
    private readonly RedisCacheService _cache;

    public RecordService(ILogger<RecordService> logger,TransactionDBContext context, RedisCacheService cache)
    {
        _logger = logger;
        _context = context;
        _cache = cache;
    }

    // Helper function for getting from cache
    public async Task<PaginatedResponse<Record>> GetOrSetCachePaginatedAsync(
        string cacheKey,
        Func<Task<List<Record>>> fetchFromDataBase,
        int offset,
        int limit,
        Func<Task<int>> totalCount,
        TimeSpan? cacheDuration = null)
    {   
        // Check if data exists in cache
        var cachedProduct = await _cache.GetCacheAsync<PaginatedResponse<Record>>(cacheKey);
        if (cachedProduct != null)
        {
           _logger.LogInformation("Cache Hit for {cacheKey}", cacheKey);
           return cachedProduct;
        }
        _logger.LogInformation("Cache Miss for {cacheKey}, fetching from DB", cacheKey);
        var count = await totalCount();
        var records = await fetchFromDataBase();
        

        var response = new PaginatedResponse<Record>
        {
            Data = records,
            TotalCount = count,
            PageSize = limit,
            CurrentPage = (offset / limit) +1,
            TotalPages = (int)Math.Ceiling(count / (double)limit),
            HasPrevious = offset > 0,
            HasNext = offset + limit < count
        };
        if(records != null )
        {
            var duration = cacheDuration ?? FlaggedDataCacheDuration;
            await _cache.SetCacheAsync(cacheKey, response, duration);
        }

        return response;
    }

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
        var records = await fetchFromDataBase();

        if(records != null )
        {
            var duration = cacheDuration ?? FlaggedDataCacheDuration;
            await _cache.SetCacheAsync(cacheKey, records, duration);
        }

        #pragma warning disable CS8603 // Possible null reference return.
        return records;                // Null reference return gets checked upon return, if null then api returns a 404
        #pragma warning restore CS8603 // Possible null reference return.
    }

    //Get paginated records
    public async Task<PaginatedResponse<Record>> GetRecords(int offset, int limit)
    {
        string cacheKey =  $"record_{offset}_{limit}";
        // GetOrSetCacheAsync returns a PaginatedResponse, not just the list
        var paginatedResponse = await GetOrSetCachePaginatedAsync(
            cacheKey,
            async () => {
                // Just fetch the raw list here
                return await _context.Records
                    .OrderBy(r => r.TransactionId)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            },
            offset,
            limit,
            async () => await _context.Records.CountAsync()
        );
        return paginatedResponse;

    }

    // Find and return a record that has a given TransactionID (also db primary key)
    public async Task<List<Record>> GetRecordById(int id)
    {
        string cacheKey = $"record_{id}";
        var paginatedResponse = await GetOrSetCacheAsync(
            cacheKey,
            async () => {
                // Just fetch the raw list here
                return await _context.Records
                    .Where(r => r.TransactionId == id)
                    .ToListAsync();
            }
        );
        return paginatedResponse;
    }


    // Get Record by Account ID
    public async Task<PaginatedResponse<Record>> GetRecordByAccountId(int id,int limit, int offset)
    {
        string cacheKey = $"record_account_{id}_limit_{limit}_offset_{offset}";
        var paginatedResponse = await GetOrSetCachePaginatedAsync(
            cacheKey,
            async () => {
                // Just fetch the raw list here
                return await _context.Records
                    .Where(r => r.AccountId == id)
                    .OrderBy(r => r.TransactionId)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            },
            offset,
            limit,
            async () => await _context.Records.Where(r => r.AccountId == id).CountAsync()
        );
        return paginatedResponse;
    }
    
    // Get Records by Recipient ID
    public async Task<PaginatedResponse<Record>> GetRecordByAccountRecipientID(int id,int limit, int offset)
    {
        string cacheKey = $"record_recipient_{id}_limit_{limit}_offset_{offset}";
        var paginatedResponse = await GetOrSetCachePaginatedAsync(
            cacheKey,
            async () => {
                // Just fetch the raw list here
                return await _context.Records
                    .Where(r => r.ReceipientId == id)
                    .OrderBy(r => r.TransactionId)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            },
            offset,
            limit,
            async () => await _context.Records.Where(r => r.ReceipientId == id).CountAsync()

        );
        return paginatedResponse;
    }

    // Get Records by Location
    public async Task<PaginatedResponse<Record>> GetRecordByLocation(string location,int limit, int offset)
    {
        string cacheKey = $"records_at_{location}_limit_{limit}_offset_{offset}";
        var records =  await GetOrSetCachePaginatedAsync(
            cacheKey,
            async () => {return await _context.Records
                .Where(r => EF.Functions.Like(r.Location.ToLower(), location.ToLower()))
                .OrderBy(r => r.TransactionId)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();},
                offset,
                limit,
                async () => await _context.Records.
                    Where(r => EF.Functions.Like(r.Location.ToLower(), location.ToLower()))
                    .CountAsync()
        );

        return records;
    }

    // Get records where fraud is true
    public async Task<PaginatedResponse<Record>> GetFraudRecords(int offset, int limit)
    {
        string cacheKey = $"fraud_records_limit_{limit}_offset_{offset}";
        var paginatedResponse = await GetOrSetCachePaginatedAsync(
            cacheKey,
            async () => {
                // Just fetch the raw list here
                return await _context.Records
                    .Where(r => r.Isfraud == true)
                    .OrderBy(r => r.TransactionId)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            },
            offset,
            limit,
            async () => await _context.Records.Where(r => r.Isfraud == true).CountAsync()

        );
        return paginatedResponse;
    }

    public async Task<PaginatedResponse<Record>> GetRecordByCategory(string category, int limit, int offset)
    {
        string cacheKey = $"records_category_{category}_limit_{limit}_offset_{offset}";
        var paginatedResponse = await GetOrSetCachePaginatedAsync(
            cacheKey,
            async () => {
                // Just fetch the raw list here
                return await _context.Records
                    .Where(r => EF.Functions.Like(r.Category.ToLower(), category.ToLower()))
                    .OrderBy(r => r.TransactionId)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            },
            offset,
            limit,
            async () => await _context.Records.Where(r => EF.Functions.Like(r.Category.ToLower(), category.ToLower())).CountAsync()

        );
        return paginatedResponse;
    }
}
