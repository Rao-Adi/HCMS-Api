using System.Collections.Concurrent;

namespace HCMS_Api.Common;

/// <summary>
/// A small in-memory cache for lookups against a slow or flaky database that are asked for over and over.
///
/// Only a successful (non-empty) answer is remembered, so a failed lookup is never turned into a cached
/// "nothing". Concurrent requests for the same key share one lookup instead of each making their own.
/// </summary>
public sealed class PositiveLookupCache
{
    private readonly TimeSpan _lifetime;
    private readonly ConcurrentDictionary<string, (string Value, DateTime ExpiresAtUtc)> _entries = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public PositiveLookupCache(TimeSpan lifetime) => _lifetime = lifetime;

    public string GetOrLookup(string key, Func<string> lookup)
    {
        if (string.IsNullOrEmpty(key))
            return lookup() ?? string.Empty;

        if (TryGet(key, out var cached))
            return cached;

        lock (_locks.GetOrAdd(key, _ => new object()))
        {
            if (TryGet(key, out cached))
                return cached;

            var value = lookup() ?? string.Empty;
            if (value.Length > 0)
                _entries[key] = (value, DateTime.UtcNow + _lifetime);
            return value;
        }
    }

    public void Invalidate(string key)
    {
        if (!string.IsNullOrEmpty(key))
            _entries.TryRemove(key, out _);
    }

    public void InvalidateByValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        foreach (var pair in _entries)
            if (pair.Value.Value == value)
                _entries.TryRemove(pair.Key, out _);
    }

    private bool TryGet(string key, out string value)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAtUtc > DateTime.UtcNow)
        {
            value = entry.Value;
            return true;
        }
        value = string.Empty;
        return false;
    }
}
