using System.Collections.Generic;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <summary>
/// Stores a log of every Lidarr artist/album request made through the standalone app - who made
/// it, what was requested, and whether Lidarr accepted it - so an admin can review request
/// activity from the settings page. Covers both individual requests from "Request Music"/"Request
/// an Album" and the bulk "Request via Lidarr" pass over a playlist's missing tracks, since that
/// feature is implemented as a series of individual artist/album requests.
/// </summary>
public interface ILidarrRequestHistoryService
{
    /// <summary>
    /// Records a Lidarr request outcome.
    /// </summary>
    /// <param name="userName">Display name of the requesting user.</param>
    /// <param name="requestType">Either "Artist" or "Album".</param>
    /// <param name="artistName">The requested artist's name.</param>
    /// <param name="albumTitle">The requested album's title, or <see langword="null"/> for an artist request.</param>
    /// <param name="succeeded">Whether Lidarr accepted the request.</param>
    /// <param name="errorMessage">The error message, when <paramref name="succeeded"/> is <see langword="false"/>.</param>
    /// <returns>The stored record.</returns>
    LidarrRequestRecordDto Add(string userName, string requestType, string artistName, string? albumTitle, bool succeeded, string? errorMessage);

    /// <summary>
    /// Gets every stored request record, newest first.
    /// </summary>
    /// <returns>All stored records.</returns>
    IReadOnlyList<LidarrRequestRecordDto> GetAll();

    /// <summary>
    /// Clears every stored request record.
    /// </summary>
    void Clear();
}
