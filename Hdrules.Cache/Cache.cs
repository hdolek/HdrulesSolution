
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Hdrules.Cache;

public interface ICacheProvider
{
    Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory);
    Task RemoveAsync(string key);
}

public class MemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache _cache;
    public MemoryCacheProvider(IMemoryCache cache) => _cache = cache;
    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue<T>(key, out var val)) return val!;
        var created = await factory();
        _cache.Set(key, created, ttl);
        return created;
    }
    public Task RemoveAsync(string key) { _cache.Remove(key); return Task.CompletedTask; }
}

public class RedisCacheProvider : ICacheProvider
{
    private readonly IDatabase _db;
    public RedisCacheProvider(IConnectionMultiplexer mux) => _db = mux.GetDatabase();
    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        var cached = await _db.StringGetAsync(key);
        if (cached.HasValue)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(cached!)!;
        }
        var created = await factory();
        var payload = System.Text.Json.JsonSerializer.Serialize(created);
        await _db.StringSetAsync(key, payload, ttl);
        return created;
    }
    public Task RemoveAsync(string key) => _db.KeyDeleteAsync(key);
}
