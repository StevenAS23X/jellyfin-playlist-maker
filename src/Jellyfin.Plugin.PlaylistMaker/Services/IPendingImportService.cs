using System;
using System.Collections.Generic;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <summary>
/// Stores, per playlist, the rows from an imported playlist that weren't in the library at import
/// time - so they can be shown again as placeholders (and re-checked against the library) the
/// next time that playlist is opened, instead of being forgotten once the browser tab closes.
/// </summary>
public interface IPendingImportService
{
    /// <summary>
    /// Gets the still-missing rows for a playlist, if any.
    /// </summary>
    /// <param name="playlistId">The playlist id.</param>
    /// <returns>The playlist's pending rows, or an empty list if none are stored.</returns>
    IReadOnlyList<ImportRowDto> Get(Guid playlistId);

    /// <summary>
    /// Replaces the stored pending rows for a playlist - called after every save/reload so the
    /// stored list always reflects exactly which placeholders are still outstanding. An empty
    /// list clears the entry entirely.
    /// </summary>
    /// <param name="playlistId">The playlist id.</param>
    /// <param name="rows">The rows still missing from the library.</param>
    void Set(Guid playlistId, IReadOnlyList<ImportRowDto> rows);
}
