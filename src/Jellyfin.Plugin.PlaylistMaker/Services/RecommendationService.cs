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
using MediaBrowser.Model.Entities;

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

        var trackQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            Recursive = true,
            IsVirtualItem = false,
            SearchTerm = query,
            Limit = limit * 3
        };
        var trackMatches = _libraryManager.GetItemList(trackQuery).OfType<Audio>();

        // SearchTerm against Audio items only matches the track's own name, not its album's -
        // separately match albums by name and pull in their tracks too, so searching "Abbey Road"
        // actually finds the album's songs instead of nothing.
        var albumQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
            Recursive = true,
            IsVirtualItem = false,
            SearchTerm = query,
            Limit = 5
        };
        var albumIds = _libraryManager.GetItemList(albumQuery).Select(a => a.Id).ToArray();

        IEnumerable<Audio> albumTrackMatches = Array.Empty<Audio>();
        if (albumIds.Length > 0)
        {
            var albumTrackQuery = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                Recursive = true,
                IsVirtualItem = false,
                AlbumIds = albumIds
            };
            albumTrackMatches = _libraryManager.GetItemList(albumTrackQuery).OfType<Audio>();
        }

        // SearchTerm against Audio items also doesn't match a track's Artists/AlbumArtists tags -
        // resolve matching artist entities the same way albums are matched above, then pull their
        // tracks directly via ArtistIds (which covers both roles), instead of the query silently
        // finding nothing (or only a few coincidental title/album hits) when someone searches for
        // an artist by name.
        var artistQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicArtist },
            Recursive = true,
            SearchTerm = query,
            Limit = 5
        };
        var artistIds = _libraryManager.GetItemList(artistQuery).Select(a => a.Id).ToArray();

        IEnumerable<Audio> artistTrackMatches = Array.Empty<Audio>();
        if (artistIds.Length > 0)
        {
            var artistTrackQuery = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                Recursive = true,
                IsVirtualItem = false,
                ArtistIds = artistIds,
                Limit = limit * 3
            };
            artistTrackMatches = _libraryManager.GetItemList(artistTrackQuery).OfType<Audio>();
        }

        // Neither of the above matches by genre - someone typing "rock" expects rock tracks back,
        // the same way picking the "Rock" chip would, not just tracks/albums literally named "rock".
        // Mirrors the album lookup above (search the indexed genre entities by name, then filter
        // tracks by GenreIds) rather than pulling every track in the library into memory to check
        // each one's genres by hand - that full-library scan was the actual cause of Search being
        // slow, since it ran on every keystroke regardless of how the query matched.
        var genreQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicGenre },
            Recursive = true,
            SearchTerm = query,
            Limit = 5
        };
        var genreIds = _libraryManager.GetItemList(genreQuery).Select(g => g.Id).ToArray();

        IEnumerable<Audio> genreTrackMatches = Array.Empty<Audio>();
        if (genreIds.Length > 0)
        {
            var genreTrackQuery = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                Recursive = true,
                IsVirtualItem = false,
                GenreIds = genreIds,
                Limit = limit * 3
            };
            genreTrackMatches = _libraryManager.GetItemList(genreTrackQuery).OfType<Audio>();
        }

        return DedupeBySong(trackMatches.Concat(albumTrackMatches).Concat(artistTrackMatches).Concat(genreTrackMatches))
            .Take(limit)
            .Select(t => TrackDtoMapper.ToDto(t))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TrackDto> Browse(Guid userId, IReadOnlyList<string> genres, IReadOnlyList<string> artists, int limit)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null || (genres.Count == 0 && artists.Count == 0))
        {
            return Array.Empty<TrackDto>();
        }

        var genreSet = new HashSet<string>(genres, StringComparer.OrdinalIgnoreCase);
        var artistSet = new HashSet<string>(artists, StringComparer.OrdinalIgnoreCase);

        return GetAllTracks(user)
            .Where(t =>
                TrackDtoMapper.GetGenreNames(t).Any(g => genreSet.Contains(g)) ||
                TrackDtoMapper.GetArtistNames(t).Any(a => artistSet.Contains(a)))
            .OrderByDescending(t => t.DateCreated)
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
            .SelectMany(TrackDtoMapper.GetGenreNames)
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
    public IReadOnlyList<(string Artist, string Title)> GetOwnedAlbums(Guid userId)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return Array.Empty<(string, string)>();
        }

        return GetAllTracks(user)
            .Select(t => (Artist: TrackDtoMapper.GetArtistNames(t).FirstOrDefault() ?? string.Empty, Title: t.AlbumEntity?.Name ?? string.Empty))
            .Where(x => x.Artist.Length > 0 && x.Title.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Releases with this many owned tracks or fewer are grouped as a single/EP rather than a
    /// full album in the artist browser - there's no formal "single" flag on local library
    /// metadata to key off of, so track count is a practical stand-in.
    /// </summary>
    private const int SingleTrackCountThreshold = 3;

    /// <inheritdoc />
    public IReadOnlyList<AlbumDto> GetArtistAlbums(Guid userId, string artist)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null || string.IsNullOrWhiteSpace(artist))
        {
            return Array.Empty<AlbumDto>();
        }

        // Resolve the artist entity via an indexed SearchTerm lookup (same as Search's artist
        // matching), not a full-library scan filtered by name in memory - this used to call
        // GetAllTracks(user), which pulls every track in the entire library into memory on every
        // single artist click regardless of that artist's actual size, and was the main reason
        // opening the artist browser felt slow on larger libraries. SearchTerm is fuzzy, so it's
        // still narrowed to an exact (case-insensitive) name match afterward - the caller already
        // has the precise artist name in hand (from a track's own Artists/AlbumArtists tag, e.g.
        // clicking an artist link on a search result), so a different, similarly-named artist
        // entity should never be picked up.
        var artistQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicArtist },
            Recursive = true,
            SearchTerm = artist,
            Limit = 10
        };
        var artistIds = _libraryManager.GetItemList(artistQuery)
            .Where(a => string.Equals(a.Name, artist, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Id)
            .ToArray();

        if (artistIds.Length == 0)
        {
            return Array.Empty<AlbumDto>();
        }

        var trackQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            Recursive = true,
            IsVirtualItem = false,
            ArtistIds = artistIds
        };

        return DedupeBySong(_libraryManager.GetItemList(trackQuery).OfType<Audio>())
            .Where(t => t.AlbumEntity is not null)
            .GroupBy(t => t.AlbumEntity!.Id)
            .Select(g =>
            {
                var album = g.First().AlbumEntity!;
                var trackCount = g.Count();
                return new AlbumDto
                {
                    Id = album.Id,
                    Name = album.Name,
                    ProductionYear = album.ProductionYear,
                    TrackCount = trackCount,
                    IsSingle = trackCount <= SingleTrackCountThreshold,
                    ImageItemId = album.HasImage(ImageType.Primary, 0) ? album.Id : (Guid?)null
                };
            })
            .OrderByDescending(a => a.ProductionYear ?? 0)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<TrackDto> GetAlbumTracks(Guid userId, Guid albumId)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return Array.Empty<TrackDto>();
        }

        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            Recursive = true,
            IsVirtualItem = false,
            AlbumIds = new[] { albumId }
        };

        return _libraryManager.GetItemList(query)
            .OfType<Audio>()
            .OrderBy(t => t.ParentIndexNumber ?? 0)
            .ThenBy(t => t.IndexNumber ?? int.MaxValue)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => TrackDtoMapper.ToDto(t))
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

            foreach (var genre in TrackDtoMapper.GetGenreNames(seedTrack))
            {
                AddWeight(genreWeights, genre, config.GenreWeight);
            }

            foreach (var artist in TrackDtoMapper.GetArtistNames(seedTrack))
            {
                AddWeight(artistWeights, artist, config.ArtistWeight);
            }
        }

        // Note: seedGenres/seedArtists (explicit chip picks) don't feed genreWeights/artistWeights
        // here - a direct pick goes through the tiered path below instead of this continuous
        // scoring model. This dictionary only ever reflects taste inferred from draft tracks.
        var candidates = allTracks.Where(t =>
            !excludeSet.Contains(t.Id) && !excludedSongKeys.Contains(TrackDtoMapper.SongKey(t)));

        // Recommendations are pulled from a pool wider than what's actually shown, then shuffled,
        // so hitting refresh gives a genuinely different set instead of the same deterministic
        // top-N reordering slightly by a small jitter every time.
        var poolSize = Math.Max(effectiveLimit * 3, effectiveLimit + 20);

        if (seedGenres.Count > 0 || seedArtists.Count > 0)
        {
            // Directly picking a genre/artist chip gets a qualitatively different mix than the
            // continuous weighted scoring below: a fixed 60% exact / 20% adjacent / 20% wildcard
            // split, so "Rock" surfaces mostly rock, some genre-adjacent picks, and a little true
            // discovery - not just "everything that scores above zero, ranked". Also folds in any
            // genres/artists from the current draft so an in-progress playlist's taste still
            // counts toward what "exact" means.
            var targetGenres = new HashSet<string>(seedGenres, StringComparer.OrdinalIgnoreCase);
            var targetArtists = new HashSet<string>(seedArtists, StringComparer.OrdinalIgnoreCase);
            foreach (var seedId in seedItemIds)
            {
                if (!tracksById.TryGetValue(seedId, out var seedTrack))
                {
                    continue;
                }

                foreach (var genre in TrackDtoMapper.GetGenreNames(seedTrack))
                {
                    targetGenres.Add(genre);
                }

                foreach (var artist in TrackDtoMapper.GetArtistNames(seedTrack))
                {
                    targetArtists.Add(artist);
                }
            }

            return BuildTieredRecommendations(candidates, allTracks, targetGenres, targetArtists, effectiveLimit);
        }

        if (genreWeights.Count == 0 && artistWeights.Count == 0)
        {
            // Cold start: no taste signal yet, surface what the user already listens to / recently added.
            var popularPool = candidates
                .Select(t => (Track: t, Score: PopularityScore(user, t)))
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Track.DateCreated)
                .Take(poolSize)
                .ToList();
            Shuffle(popularPool);

            return popularPool
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

            foreach (var genre in TrackDtoMapper.GetGenreNames(track))
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

            var reason = bestArtistMatch is not null
                ? $"Because you like {bestArtistMatch}"
                : bestGenreMatch is not null
                    ? $"Similar genre: {bestGenreMatch}"
                    : "Similar to your playlist";

            scored.Add((track, score, reason));
        }

        var scoredPool = scored
            .OrderByDescending(x => x.Score)
            .Take(poolSize)
            .ToList();
        Shuffle(scoredPool);

        return scoredPool
            .Take(effectiveLimit)
            .Select(x => TrackDtoMapper.ToDto(x.Track, x.Reason))
            .ToList();
    }

    /// <summary>
    /// Builds recommendations for a direct genre/artist pick as three fixed-proportion tiers:
    /// ~60% tracks that directly match a picked genre or artist, ~20% "adjacent" tracks (genres
    /// that frequently co-occur with the pick in this library, or artists who share those
    /// genres), and ~20% wildcard tracks unrelated to the pick at all, for serendipitous
    /// discovery. Each tier is internally shuffled from a scored pool the same way the
    /// draft-based recommendations are, so repeated refreshes still vary.
    /// </summary>
    private IReadOnlyList<TrackDto> BuildTieredRecommendations(
        IEnumerable<Audio> candidates,
        IReadOnlyList<Audio> allTracks,
        HashSet<string> targetGenres,
        HashSet<string> targetArtists,
        int effectiveLimit)
    {
        var adjacentGenres = ComputeAdjacentGenres(allTracks, targetGenres);
        var adjacentArtists = ComputeAdjacentArtists(allTracks, targetGenres, targetArtists);

        var exact = new List<Audio>();
        var adjacent = new List<Audio>();
        var wildcard = new List<Audio>();

        foreach (var track in candidates)
        {
            var trackGenres = TrackDtoMapper.GetGenreNames(track).ToList();
            var trackArtists = TrackDtoMapper.GetArtistNames(track).ToList();

            if (trackGenres.Any(targetGenres.Contains) || trackArtists.Any(targetArtists.Contains))
            {
                exact.Add(track);
            }
            else if (trackGenres.Any(adjacentGenres.Contains) || trackArtists.Any(adjacentArtists.Contains))
            {
                adjacent.Add(track);
            }
            else
            {
                wildcard.Add(track);
            }
        }

        Shuffle(exact);
        Shuffle(adjacent);
        Shuffle(wildcard);

        var exactCount = (int)Math.Round(effectiveLimit * 0.6, MidpointRounding.AwayFromZero);
        var adjacentCount = (int)Math.Round(effectiveLimit * 0.2, MidpointRounding.AwayFromZero);
        var wildcardCount = Math.Max(effectiveLimit - exactCount - adjacentCount, 0);

        var result = new List<TrackDto>(effectiveLimit);
        result.AddRange(exact.Take(exactCount).Select(t => TrackDtoMapper.ToDto(t, ExactReason(t, targetGenres, targetArtists))));
        result.AddRange(adjacent.Take(adjacentCount).Select(t => TrackDtoMapper.ToDto(t, AdjacentReason(t, adjacentGenres, adjacentArtists))));
        result.AddRange(wildcard.Take(wildcardCount).Select(t => TrackDtoMapper.ToDto(t, "Something different")));

        // Backfill from whichever tier has leftovers if a small library leaves us short of
        // effectiveLimit overall (e.g. too few wildcard candidates), preferring closer tiers first.
        var shortfall = effectiveLimit - result.Count;
        if (shortfall > 0)
        {
            var leftovers = exact.Skip(exactCount)
                .Concat(adjacent.Skip(adjacentCount))
                .Concat(wildcard.Skip(wildcardCount))
                .Take(shortfall);
            result.AddRange(leftovers.Select(t => TrackDtoMapper.ToDto(t, "More like this")));
        }

        return result;
    }

    /// <summary>
    /// Finds genres that frequently co-occur with the target genres on the same tracks in this
    /// library (e.g. tracks tagged "Rock" that are also often tagged "Alternative Rock"), as a
    /// cheap stand-in for genre similarity without any external taxonomy.
    /// </summary>
    private static HashSet<string> ComputeAdjacentGenres(IReadOnlyList<Audio> allTracks, HashSet<string> targetGenres, int topPerGenre = 6)
    {
        var coOccurrence = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var track in allTracks)
        {
            var trackGenres = TrackDtoMapper.GetGenreNames(track).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var matchedTargets = trackGenres.Where(targetGenres.Contains);

            foreach (var target in matchedTargets)
            {
                if (!coOccurrence.TryGetValue(target, out var bag))
                {
                    bag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    coOccurrence[target] = bag;
                }

                foreach (var other in trackGenres)
                {
                    if (targetGenres.Contains(other))
                    {
                        continue;
                    }

                    bag[other] = bag.TryGetValue(other, out var count) ? count + 1 : 1;
                }
            }
        }

        var adjacent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bag in coOccurrence.Values)
        {
            foreach (var genre in bag.OrderByDescending(kv => kv.Value).Take(topPerGenre).Select(kv => kv.Key))
            {
                adjacent.Add(genre);
            }
        }

        return adjacent;
    }

    /// <summary>
    /// Finds artists "adjacent" to the target artists: other artists whose tracks share the most
    /// genres with the target artists' tracks (or, if only genres were picked with no artist,
    /// artists who work in the adjacent genres). Also a co-occurrence stand-in - Lidarr-style
    /// "similar artist" data isn't something this library has on its own.
    /// </summary>
    private static HashSet<string> ComputeAdjacentArtists(
        IReadOnlyList<Audio> allTracks,
        HashSet<string> targetGenres,
        HashSet<string> targetArtists,
        int topArtists = 15)
    {
        var relatedGenres = new HashSet<string>(targetGenres, StringComparer.OrdinalIgnoreCase);

        if (targetArtists.Count > 0)
        {
            foreach (var track in allTracks)
            {
                if (TrackDtoMapper.GetArtistNames(track).Any(targetArtists.Contains))
                {
                    foreach (var genre in TrackDtoMapper.GetGenreNames(track))
                    {
                        relatedGenres.Add(genre);
                    }
                }
            }
        }

        if (relatedGenres.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var overlapCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var track in allTracks)
        {
            var overlap = TrackDtoMapper.GetGenreNames(track).Count(relatedGenres.Contains);
            if (overlap == 0)
            {
                continue;
            }

            foreach (var artist in TrackDtoMapper.GetArtistNames(track))
            {
                if (targetArtists.Contains(artist))
                {
                    continue;
                }

                overlapCounts[artist] = overlapCounts.TryGetValue(artist, out var count) ? count + overlap : overlap;
            }
        }

        return new HashSet<string>(
            overlapCounts.OrderByDescending(kv => kv.Value).Take(topArtists).Select(kv => kv.Key),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ExactReason(Audio track, HashSet<string> targetGenres, HashSet<string> targetArtists)
    {
        var artist = TrackDtoMapper.GetArtistNames(track).FirstOrDefault(targetArtists.Contains);
        if (artist is not null)
        {
            return $"Because you like {artist}";
        }

        var genre = TrackDtoMapper.GetGenreNames(track).FirstOrDefault(targetGenres.Contains);
        return genre is not null ? $"Tagged {genre}" : "Matches your pick";
    }

    private static string AdjacentReason(Audio track, HashSet<string> adjacentGenres, HashSet<string> adjacentArtists)
    {
        var artist = TrackDtoMapper.GetArtistNames(track).FirstOrDefault(adjacentArtists.Contains);
        if (artist is not null)
        {
            return $"Similar artist: {artist}";
        }

        var genre = TrackDtoMapper.GetGenreNames(track).FirstOrDefault(adjacentGenres.Contains);
        return genre is not null ? $"Genre-adjacent: {genre}" : "Related pick";
    }

    /// <summary>
    /// In-place Fisher-Yates shuffle.
    /// </summary>
    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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
