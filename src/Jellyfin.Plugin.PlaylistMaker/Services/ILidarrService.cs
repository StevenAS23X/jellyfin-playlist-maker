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
    /// Searches Lidarr's metadata source for albums/singles matching the given title.
    /// </summary>
    /// <param name="term">The search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching albums.</returns>
    Task<IReadOnlyList<LidarrAlbumDto>> SearchAlbums(string term, CancellationToken cancellationToken);

    /// <summary>
    /// Requests a single album be added to Lidarr, without pulling in the rest of the artist's
    /// discography: the artist is added (if not already present) with monitoring limited to just
    /// this album, which is then monitored and searched for immediately. This matches how
    /// indexers actually distribute music - album by album or as standalone singles, never as a
    /// complete discography in one release.
    /// </summary>
    /// <param name="foreignAlbumId">The MusicBrainz release-group id from a search result.</param>
    /// <param name="artistForeignArtistId">The owning artist's MusicBrainz id.</param>
    /// <param name="artistName">The artist name, for a clearer error message on failure.</param>
    /// <param name="albumTitle">The album title, for a clearer error message on failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the request has been made.</returns>
    Task RequestAlbum(string foreignAlbumId, string artistForeignArtistId, string artistName, string albumTitle, CancellationToken cancellationToken);

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
