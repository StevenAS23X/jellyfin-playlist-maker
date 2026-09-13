using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <inheritdoc />
public class LidarrRequestHistoryService : ILidarrRequestHistoryService
{
    private const string StateFileName = "lidarr-request-history.json";
    private const int MaxStoredRequests = 1000;
    private readonly object _lock = new();

    private static string DataFolderPath => Plugin.Instance!.DataFolderPath;

    /// <inheritdoc />
    public LidarrRequestRecordDto Add(string userName, string requestType, string artistName, string? albumTitle, bool succeeded, string? errorMessage)
    {
        var record = new LidarrRequestRecordDto
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            RequestType = requestType,
            ArtistName = artistName,
            AlbumTitle = albumTitle,
            Succeeded = succeeded,
            ErrorMessage = succeeded ? null : errorMessage,
            RequestedAt = DateTime.UtcNow
        };

        lock (_lock)
        {
            var requests = Load();
            requests.Insert(0, record);

            // Cap how many are kept so an unattended install doesn't grow this file forever -
            // oldest entries fall off first, same as CustomRequestService's bounded log.
            if (requests.Count > MaxStoredRequests)
            {
                requests.RemoveRange(MaxStoredRequests, requests.Count - MaxStoredRequests);
            }

            Save(requests);
        }

        return record;
    }

    /// <inheritdoc />
    public IReadOnlyList<LidarrRequestRecordDto> GetAll()
    {
        lock (_lock)
        {
            return Load().OrderByDescending(r => r.RequestedAt).ToList();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            Save(new List<LidarrRequestRecordDto>());
        }
    }

    private static List<LidarrRequestRecordDto> Load()
    {
        var path = Path.Combine(DataFolderPath, StateFileName);
        try
        {
            if (!File.Exists(path))
            {
                return new List<LidarrRequestRecordDto>();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<LidarrRequestRecordDto>>(json)
                ?? new List<LidarrRequestRecordDto>();
        }
        catch (IOException)
        {
            return new List<LidarrRequestRecordDto>();
        }
        catch (JsonException)
        {
            return new List<LidarrRequestRecordDto>();
        }
    }

    private static void Save(List<LidarrRequestRecordDto> requests)
    {
        try
        {
            Directory.CreateDirectory(DataFolderPath);
            File.WriteAllText(Path.Combine(DataFolderPath, StateFileName), JsonSerializer.Serialize(requests));
        }
        catch (IOException)
        {
            // Best-effort persistence - losing unsaved history on a read-only data folder is an
            // acceptable fallback, same tradeoff as RequestRateLimiter/CustomRequestService.
        }
    }
}
