using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.PlaylistMaker.Configuration;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <inheritdoc />
public class RequestRateLimiter : IRequestRateLimiter
{
    private const string StateFileName = "request-history.json";
    private readonly object _lock = new();

    private static PluginConfiguration Config => Plugin.Instance!.Configuration;

    private static string DataFolderPath => Plugin.Instance!.DataFolderPath;

    /// <inheritdoc />
    public bool TryRecordRequest(Guid userId, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        var maxRequests = Config.MaxRequestsPerUser;
        if (maxRequests <= 0)
        {
            return true;
        }

        var window = TimeSpan.FromHours(Math.Max(Config.RequestWindowHours, 1));
        var now = DateTime.UtcNow;
        var cutoff = now - window;
        var key = userId.ToString("N");

        lock (_lock)
        {
            var state = Load();

            // Prune every user's stale entries (not just the caller's) so the file stays small.
            foreach (var otherKey in state.Keys.ToList())
            {
                state[otherKey] = state[otherKey].Where(t => t > cutoff).ToList();
                if (state[otherKey].Count == 0)
                {
                    state.Remove(otherKey);
                }
            }

            state.TryGetValue(key, out var timestamps);
            timestamps ??= new List<DateTime>();

            if (timestamps.Count >= maxRequests)
            {
                var oldest = timestamps.Min();
                var remaining = (oldest + window) - now;
                retryAfter = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                return false;
            }

            timestamps.Add(now);
            state[key] = timestamps;
            Save(state);
            return true;
        }
    }

    private static Dictionary<string, List<DateTime>> Load()
    {
        var path = Path.Combine(DataFolderPath, StateFileName);
        try
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, List<DateTime>>();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, List<DateTime>>>(json)
                ?? new Dictionary<string, List<DateTime>>();
        }
        catch (IOException)
        {
            return new Dictionary<string, List<DateTime>>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, List<DateTime>>();
        }
    }

    private static void Save(Dictionary<string, List<DateTime>> state)
    {
        try
        {
            Directory.CreateDirectory(DataFolderPath);
            File.WriteAllText(Path.Combine(DataFolderPath, StateFileName), JsonSerializer.Serialize(state));
        }
        catch (IOException)
        {
            // Best-effort persistence - an in-memory-only limit for this process run is an
            // acceptable fallback if the plugin data folder isn't writable.
        }
    }
}
