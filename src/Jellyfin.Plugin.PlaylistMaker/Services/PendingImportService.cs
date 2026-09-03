using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <inheritdoc />
public class PendingImportService : IPendingImportService
{
    private const string StateFileName = "pending-imports.json";
    private readonly object _lock = new();

    private static string DataFolderPath => Plugin.Instance!.DataFolderPath;

    /// <inheritdoc />
    public IReadOnlyList<ImportRowDto> Get(Guid playlistId)
    {
        lock (_lock)
        {
            var state = Load();
            return state.TryGetValue(playlistId.ToString("N"), out var rows) ? rows : Array.Empty<ImportRowDto>();
        }
    }

    /// <inheritdoc />
    public void Set(Guid playlistId, IReadOnlyList<ImportRowDto> rows)
    {
        lock (_lock)
        {
            var state = Load();
            var key = playlistId.ToString("N");

            if (rows.Count == 0)
            {
                state.Remove(key);
            }
            else
            {
                state[key] = new List<ImportRowDto>(rows);
            }

            Save(state);
        }
    }

    private static Dictionary<string, List<ImportRowDto>> Load()
    {
        var path = Path.Combine(DataFolderPath, StateFileName);
        try
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, List<ImportRowDto>>();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, List<ImportRowDto>>>(json)
                ?? new Dictionary<string, List<ImportRowDto>>();
        }
        catch (IOException)
        {
            return new Dictionary<string, List<ImportRowDto>>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, List<ImportRowDto>>();
        }
    }

    private static void Save(Dictionary<string, List<ImportRowDto>> state)
    {
        try
        {
            Directory.CreateDirectory(DataFolderPath);
            File.WriteAllText(Path.Combine(DataFolderPath, StateFileName), JsonSerializer.Serialize(state));
        }
        catch (IOException)
        {
            // Best-effort persistence - losing not-yet-saved pending rows on a read-only data
            // folder is an acceptable fallback, same tradeoff as CustomRequestService.
        }
    }
}
