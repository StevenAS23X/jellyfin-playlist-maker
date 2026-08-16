# Jellyfin Playlist Maker

A Jellyfin server plugin that adds a **Playlist Maker** page to your Jellyfin sidebar —
a fast, Spotify/Apple-Music-style playlist builder for your own music library, with a
live "Recommended For You" panel driven by genre and artist matching.

## Why

Building playlists in the stock Jellyfin web UI or apps is slow, and it's hard to know
what's even in your library while you're picking tracks. This plugin fixes both:

- **One search box** across tracks, artists, and albums, with instant add-to-playlist.
- **Genre / artist quick-pick chips** so you can start a playlist from a vibe instead of
  hunting through your library first.
- **A live recommendations panel** that updates as you add tracks — similar to how
  Spotify/Apple Music suggest similar songs while you build a playlist. It scores every
  track in your library against the genres and artists already in your draft (plus any
  genre/artist chips you picked), so it gets more relevant the more you add.
- **Cold start**: with an empty draft it falls back to your most-played and
  most-recently-added tracks, so the panel is never empty.
- Saves straight to a real Jellyfin playlist via the built-in playlist manager — no
  separate app, nothing to sync.

## How it's integrated

This ships as a normal Jellyfin **server plugin** (a .dll dropped into your server's
plugin folder), not a modified web client. It registers a page that Jellyfin shows in
the main navigation sidebar (`EnableInMainMenu`, added in Jellyfin 10.9) — right next to
Movies, Music, etc. On servers older than 10.9 (or if your client build doesn't honor
that flag) the page is still reachable, just from **Dashboard → Plugins → Playlist
Maker**, which is the normal way Jellyfin plugin pages work.

Going further — actually injecting a button into Jellyfin's built-in Music library
view — isn't possible through the plugin API; it would require patching and
maintaining a custom build of `jellyfin-web` that breaks on every server update. We
deliberately avoided that (see the discussion in this repo's history) in favor of the
sidebar page, which is safe across updates and still one click away from Music.

## Project layout

```
src/Jellyfin.Plugin.PlaylistMaker/
  Plugin.cs                        Plugin entry point, registers the sidebar page
  PluginServiceRegistrator.cs      DI registration for the recommendation service
  Configuration/
    PluginConfiguration.cs         Tunable weights (see below)
  Api/
    PlaylistMakerController.cs     REST endpoints used by the builder UI
    Dto/                           Request/response models
  Services/
    IRecommendationService.cs
    RecommendationService.cs       Genre/artist scoring + cold-start fallback
  Web/
    playlistmaker.html/.js/.css    The builder UI (search, draft, recommendations)
  build.yaml / meta.json           Plugin manifest metadata
```

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build src/Jellyfin.Plugin.PlaylistMaker/Jellyfin.Plugin.PlaylistMaker.csproj -c Release
```

The build targets `Jellyfin.Controller` `10.9.*`. If your server runs a different
Jellyfin version, update the `PackageReference` version in the `.csproj` to match, and
adjust `targetAbi` in `build.yaml` / `meta.json`.

A GitHub Actions workflow (`.github/workflows/build.yml`) builds the plugin on every
push, so you can check the Actions tab for compile status without needing a local
.NET install.

> **Note on this PR:** the sandbox this was developed in has network egress limited to
> a small allowlist (npm/PyPI/NuGet/GitHub) and could not reach `dot.net` or
> Microsoft's SDK download CDN, so the code could not be compiled locally before
> pushing. The CI workflow above is the real compile check — see its status on this
> branch, and treat any red build as something to fix before merging. The Jellyfin
> server API surface used here (`ILibraryManager`, `IPlaylistManager`,
> `IUserDataManager`, `PluginPageInfo`, etc.) was written from the published Jellyfin
> server source, but exact method signatures do drift between versions, so double-check
> the CI result.

## Installing

1. Build the plugin (or grab `Jellyfin.Plugin.PlaylistMaker.dll` from a CI run's
   artifacts).
2. Copy it into a new folder under your Jellyfin server's plugin directory, e.g.
   `<jellyfin-data>/plugins/Playlist Maker/Jellyfin.Plugin.PlaylistMaker.dll`.
3. Restart Jellyfin.
4. Look for **Playlist Maker** in the sidebar (or under Dashboard → Plugins).

## Configuration

Under Dashboard → Plugins → Playlist Maker you can tune:

| Setting | Default | Effect |
|---|---|---|
| Max recommendations | 30 | Upper bound on how many tracks the panel returns per request |
| Artist weight | 1.6 | How strongly a shared artist boosts a track's score vs. genre |
| Genre weight | 1.0 | How strongly a shared genre boosts a track's score |
| Boost by play count | on | Small nudge toward tracks you already listen to |

## API

All endpoints live under `/PlaylistMaker` and require a normal Jellyfin auth token
(same as any other Jellyfin API call):

| Endpoint | Purpose |
|---|---|
| `GET /PlaylistMaker/Search?userId=&query=&limit=` | Free-text search across tracks/artists/albums |
| `GET /PlaylistMaker/Genres?userId=` | Distinct genres in the library |
| `GET /PlaylistMaker/Artists?userId=` | Distinct artists in the library |
| `GET /PlaylistMaker/Recommendations?userId=&seedItemIds=&seedGenres=&seedArtists=&excludeItemIds=&limit=` | Ranked "more like this" tracks |
| `POST /PlaylistMaker/Playlists` | Create a playlist from a name + ordered track id list |
| `POST /PlaylistMaker/Playlists/{playlistId}/Items` | Append tracks to an existing playlist |

## Recommendation algorithm

For each track already in your draft (or each genre/artist chip you picked), the
service accumulates a weight per genre and per artist. Every other track in the library
is scored by summing the weights of its own genres/artists that overlap with that
profile, plus a small play-count nudge and a touch of random jitter (so the panel isn't
static between refreshes) — then the highest scoring tracks are returned, each tagged
with a short reason ("Because you like X", "Similar genre: Y"). With no draft and no
chips picked yet, it falls back to your most-played / most-recently-added tracks so the
panel always has something to show.

## License

No license file has been added yet — add one before publishing this publicly if you
plan to distribute it.
