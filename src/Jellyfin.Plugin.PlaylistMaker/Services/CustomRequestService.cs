using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <inheritdoc />
public class CustomRequestService : ICustomRequestService
{
    private const string StateFileName = "custom-requests.json";
    private const int MaxStoredRequests = 500;
    private readonly object _lock = new();

    private static string DataFolderPath => Plugin.Instance!.DataFolderPath;

    /// <inheritdoc />
    public CustomRequestRecordDto Add(string userName, string link, string? note)
    {
        var record = new CustomRequestRecordDto
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Link = link,
            Note = note,
            SubmittedAt = DateTime.UtcNow
        };

        lock (_lock)
        {
            var requests = Load();
            requests.Insert(0, record);

            // Cap how many are kept so an unattended install doesn't grow this file forever -
            // oldest entries fall off first, same as a bounded log.
            if (requests.Count > MaxStoredRequests)
            {
                requests.RemoveRange(MaxStoredRequests, requests.Count - MaxStoredRequests);
            }

            Save(requests);
        }

        return record;
    }

    /// <inheritdoc />
    public IReadOnlyList<CustomRequestRecordDto> GetAll()
    {
        lock (_lock)
        {
            return Load().OrderByDescending(r => r.SubmittedAt).ToList();
        }
    }

    /// <inheritdoc />
    public bool Remove(Guid id)
    {
        lock (_lock)
        {
            var requests = Load();
            var removed = requests.RemoveAll(r => r.Id == id) > 0;
            if (removed)
            {
                Save(requests);
            }

            return removed;
        }
    }

    private static List<CustomRequestRecordDto> Load()
    {
        var path = Path.Combine(DataFolderPath, StateFileName);
        try
        {
            if (!File.Exists(path))
            {
                return new List<CustomRequestRecordDto>();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<CustomRequestRecordDto>>(json)
                ?? new List<CustomRequestRecordDto>();
        }
        catch (IOException)
        {
            return new List<CustomRequestRecordDto>();
        }
        catch (JsonException)
        {
            return new List<CustomRequestRecordDto>();
        }
    }

    private static void Save(List<CustomRequestRecordDto> requests)
    {
        try
        {
            Directory.CreateDirectory(DataFolderPath);
            File.WriteAllText(Path.Combine(DataFolderPath, StateFileName), JsonSerializer.Serialize(requests));
        }
        catch (IOException)
        {
            // Best-effort persistence - losing unsaved requests on a read-only data folder is an
            // acceptable fallback, same tradeoff as RequestRateLimiter.
        }
    }
}
