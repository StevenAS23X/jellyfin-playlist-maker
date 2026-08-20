using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <summary>
/// Talks to a configured Lidarr instance so users can request an artist be added to the library,
/// the way Jellyseerr/Overseerr request movies and shows via Radarr/Sonarr.
/// </summary>
public interface ILidarrService
{
    /// <summary>
    /// Gets a value indicating whether Lidarr is enabled and has the minimum settings needed to
    /// make requests (base URL, API key, root folder, quality/metadata profiles).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Searches Lidarr's metadata source for artists matching the given name.
    /// </summary>
    /// <param name="term">The search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching artists.</returns>
    Task<IReadOnlyList<LidarrArtistDto>> SearchArtists(string term, CancellationToken cancellationToken);

    /// <summary>
    /// Adds an artist to Lidarr, monitored and set to search for missing albums immediately -
    /// the same as clicking "Add" in Lidarr's own UI.
    /// </summary>
    /// <param name="foreignArtistId">The MusicBrainz id from a search result.</param>
    /// <param name="artistName">The artist name, for a clearer error message on failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the request has been made.</returns>
    Task RequestArtist(string foreignArtistId, string artistName, CancellationToken cancellationToken);

    /// <summary>
    /// Gets Lidarr's configured root folders, for the admin settings dropdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Root folder options, keyed by folder path.</returns>
    Task<IReadOnlyList<LidarrOptionDto>> GetRootFolders(CancellationToken cancellationToken);

    /// <summary>
    /// Gets Lidarr's configured quality profiles, for the admin settings dropdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Quality profile options, keyed by id.</returns>
    Task<IReadOnlyList<LidarrOptionDto>> GetQualityProfiles(CancellationToken cancellationToken);

    /// <summary>
    /// Gets Lidarr's configured metadata profiles, for the admin settings dropdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata profile options, keyed by id.</returns>
    Task<IReadOnlyList<LidarrOptionDto>> GetMetadataProfiles(CancellationToken cancellationToken);
}
