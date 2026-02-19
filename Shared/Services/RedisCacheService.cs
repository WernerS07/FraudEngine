using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using StackExchange.Redis;

namespace Shared.Services
{
    public class RedisCacheService
    {
        private readonly IDistributedCache _cache;
        private readonly IConnectionMultiplexer? _redis;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly string _instanceName;

        public RedisCacheService(
            IDistributedCache cache, 
            IConnectionMultiplexer redis,
            ILogger<RedisCacheService> logger,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _cache = cache;
            _redis = redis;
            _logger = logger;
            _instanceName = configuration["Redis:InstanceName"] ?? "Records";
        }

        public async Task SetCacheAsync<T>(string key, T value, TimeSpan expirationTime)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expirationTime
            };

            var jsonData = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, jsonData, options);
        }

        public async Task<T?> GetCacheAsync<T>(string key)
        {
            var jsonData = await _cache.GetStringAsync(key);
            return jsonData is null ? default : JsonSerializer.Deserialize<T?>(jsonData);
        }

        public async Task RemoveCacheAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }

        public async Task ClearAllInstanceCachesAsync()
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis connection not available for clearing instance caches");
                return;
            }

            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var pattern = $"{_instanceName}*";
                
                _logger.LogInformation("Clearing all caches with instance prefix: {InstanceName}", _instanceName);
                
                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Length > 0)
                {
                    var db = _redis.GetDatabase();
                    await db.KeyDeleteAsync(keys);
                    _logger.LogInformation("Cleared {Count} cache keys", keys.Length);
                }
                else
                {
                    _logger.LogInformation("No cache keys found to clear");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing instance caches");
            }
        }

        public async Task ClearCacheByPatternAsync(string pattern)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis connection not available for pattern deletion");
                return;
            }

            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var fullPattern = $"{_instanceName}{pattern}";
                
                _logger.LogInformation("Clearing cache keys matching pattern: {Pattern}", fullPattern);
                
                var keys = server.Keys(pattern: fullPattern).ToArray();
                if (keys.Length > 0)
                {
                    var db = _redis.GetDatabase();
                    await db.KeyDeleteAsync(keys);
                    _logger.LogInformation("Cleared {Count} cache keys", keys.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache by pattern: {Pattern}", pattern);
            }
        }
    }
}
