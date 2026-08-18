using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;
using Jellyfin.Plugin.PlaylistMaker.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <inheritdoc />
public class RecommendationService : IRecommendationService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly Random _random = new Random();

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationService"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    public RecommendationService(
        ILibraryManager libraryManager,
        IUserManager userManager,
        IUserDataManager userDataManager)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _userDataManager = userDataManager;
    }

    private static PluginConfiguration Config =>
        Plugin.Instance!.Configuration;

    /// <inheritdoc />
    public IReadOnlyList<TrackDto> Search(Guid userId, string query, int limit)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<TrackDto>();
        }

        var libraryQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            Recursive = true,
            IsVirtualItem = false,
            SearchTerm = query,
            Limit = limit * 3
        };

        return DedupeBySong(_libraryManager.GetItemList(libraryQuery).OfType<Audio>())
            .Take(limit)
            .Select(t => TrackDtoMapper.ToDto(t))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetGenres(Guid userId)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return Array.Empty<string>();
        }

        return GetAllTracks(user)
            .SelectMany(t => t.Genres ?? Array.Empty<string>())
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetArtists(Guid userId)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return Array.Empty<string>();
        }

        return GetAllTracks(user)
            .SelectMany(TrackDtoMapper.GetArtistNames)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TrackDto> GetRecommendations(
        Guid userId,
        IReadOnlyList<Guid> seedItemIds,
        IReadOnlyList<string> seedGenres,
        IReadOnlyList<string> seedArtists,
        IReadOnlyList<Guid> excludeItemIds,
        int limit)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return Array.Empty<TrackDto>();
        }

        var config = Config;
        var effectiveLimit = limit <= 0 ? config.MaxRecommendations : Math.Min(limit, config.MaxRecommendations);

        var allTracks = GetAllTracks(user).ToList();
        var tracksById = allTracks.ToDictionary(t => t.Id);

        var excludeSet = new HashSet<Guid>(excludeItemIds);
        foreach (var id in seedItemIds)
        {
            excludeSet.Add(id);
        }

        // Seed tracks may not be in allTracks (e.g. a track loaded from an existing playlist that
        // GetAllTracks' current paging/ordering didn't happen to include); also exclude every other
        // library item that's the same underlying song, so duplicate imports of an already-picked
        // track don't keep reappearing as "recommendations".
        var excludedSongKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in excludeSet)
        {
            if (tracksById.TryGetValue(id, out var excludedTrack))
            {
                excludedSongKeys.Add(TrackDtoMapper.SongKey(excludedTrack));
            }
        }

        var genreWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var artistWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        void AddWeight(Dictionary<string, double> bag, string key, double weight)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            bag[key] = bag.TryGetValue(key, out var existing) ? existing + weight : weight;
        }

        foreach (var seedId in seedItemIds)
        {
            if (!tracksById.TryGetValue(seedId, out var seedTrack))
            {
                continue;
            }

            foreach (var genre in seedTrack.Genres ?? Array.Empty<string>())
            {
                AddWeight(genreWeights, genre, config.GenreWeight);
            }

            foreach (var artist in TrackDtoMapper.GetArtistNames(seedTrack))
            {
                AddWeight(artistWeights, artist, config.ArtistWeight);
            }
        }

        foreach (var genre in seedGenres)
        {
            AddWeight(genreWeights, genre, config.GenreWeight * 2);
        }

        foreach (var artist in seedArtists)
        {
            AddWeight(artistWeights, artist, config.ArtistWeight * 2);
        }

        var candidates = allTracks.Where(t =>
            !excludeSet.Contains(t.Id) && !excludedSongKeys.Contains(TrackDtoMapper.SongKey(t)));

        if (genreWeights.Count == 0 && artistWeights.Count == 0)
        {
            // Cold start: no taste signal yet, surface what the user already listens to / recently added.
            return candidates
                .Select(t => (Track: t, Score: PopularityScore(user, t)))
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Track.DateCreated)
                .Take(effectiveLimit)
                .Select(x => TrackDtoMapper.ToDto(x.Track, "Popular in your library"))
                .ToList();
        }

        var scored = new List<(Audio Track, double Score, string Reason)>();

        foreach (var track in candidates)
        {
            double score = 0;
            string? bestArtistMatch = null;
            double bestArtistWeight = 0;
            string? bestGenreMatch = null;
            double bestGenreWeight = 0;

            foreach (var artist in TrackDtoMapper.GetArtistNames(track))
            {
                if (artistWeights.TryGetValue(artist, out var w))
                {
                    score += w;
                    if (w > bestArtistWeight)
                    {
                        bestArtistWeight = w;
                        bestArtistMatch = artist;
                    }
                }
            }

            foreach (var genre in track.Genres ?? Array.Empty<string>())
            {
                if (genreWeights.TryGetValue(genre, out var w))
                {
                    score += w;
                    if (w > bestGenreWeight)
                    {
                        bestGenreWeight = w;
                        bestGenreMatch = genre;
                    }
                }
            }

            if (score <= 0)
            {
                continue;
            }

            if (config.BoostByPlayCount)
            {
                score += PopularityScore(user, track) * 0.05;
            }

            // Small jitter so repeated requests with the same seed surface some variety,
            // the way Spotify's suggestions shuffle a little between visits.
            score += _random.NextDouble() * 0.05;

            var reason = bestArtistMatch is not null
                ? $"Because you like {bestArtistMatch}"
                : bestGenreMatch is not null
                    ? $"Similar genre: {bestGenreMatch}"
                    : "Similar to your playlist";

            scored.Add((track, score, reason));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .Take(effectiveLimit)
            .Select(x => TrackDtoMapper.ToDto(x.Track, x.Reason))
            .ToList();
    }

    private IEnumerable<Audio> GetAllTracks(User user)
    {
        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            Recursive = true,
            IsVirtualItem = false
        };

        return DedupeBySong(_libraryManager.GetItemList(query).OfType<Audio>());
    }

    /// <summary>
    /// Collapses duplicate library entries of the same underlying song (e.g. the same album
    /// imported twice, or a track present under two library paths) down to a single instance, so
    /// they don't show up as repeated rows in search/recommendations and so adding one instance to
    /// a playlist correctly excludes the others from future recommendations too.
    /// </summary>
    private static IEnumerable<Audio> DedupeBySong(IEnumerable<Audio> tracks)
    {
        return tracks
            .GroupBy(TrackDtoMapper.SongKey, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(t => t.DateCreated).First());
    }

    private double PopularityScore(User user, Audio track)
    {
        var userData = _userDataManager.GetUserData(user, track);
        return userData?.PlayCount ?? 0;
    }
}
