# Jellyfin Playlist Maker

A Jellyfin server plugin that adds a **Playlist Maker** page under Dashboard → Plugins —
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
plugin folder), not a modified web client. It registers a page the standard way any
Jellyfin plugin does, which surfaces it under **Dashboard → Plugins → Playlist Maker**.

Note: `PluginPageInfo.EnableInMainMenu` does *not* place a page in the main app
navigation next to Movies/Music, despite what an earlier version of this README
claimed — checking the actual `jellyfin-web` source, that flag only affects which of a
plugin's registered pages its "Settings" button links to on the admin Plugins screen.
Dashboard → Plugins is genuinely the only place a plugin page can appear without
patching `jellyfin-web` itself. Actually injecting a button into Jellyfin's built-in
Music library view would require exactly that — maintaining a custom `jellyfin-web`
build (or relying on a live file-patching plugin like `FileTransformation`, which some
servers already use for re-skinning) — both fragile across server updates, so this
project deliberately doesn't attempt it.

## Project layout

```
src/Jellyfin.Plugin.PlaylistMaker/
  Plugin.cs                        Plugin entry point, registers the Dashboard page
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
> Microsoft's SDK download CDN, so the code could not be compiled locally. Instead it
> was cross-checked against the actual Jellyfin `v10.9.11` server source (cloned from
> GitHub) for every non-obvious API — `IPlaylistManager`, `PlaylistCreationRequest`,
> `BaseItemKind`, `MediaType`, `PluginPageInfo`, etc. — and verified compiling via the
> CI workflow above, which is green as of the latest commit on this branch.

## Installing

### Option A: plugin repository (recommended — installs/updates like a catalog plugin)

This repo publishes itself as a self-hosted Jellyfin plugin repository, the same
mechanism the plugins in [awesome-jellyfin](https://github.com/awesome-jellyfin/awesome-jellyfin)
use. Once set up, there's no manual file copying, ever — Jellyfin's own plugin catalog
handles install and future updates.

1. In Jellyfin: Dashboard → Plugins → Repositories → **Add Repository**.
2. Name it whatever you like, and set the URL to:
   ```
   https://stevenas23x.github.io/jellyfin-playlist-maker/manifest.json
   ```
3. Go to Dashboard → Plugins → Catalog, find **Playlist Maker**, install it, restart
   Jellyfin.

This URL is generated automatically by `.github/workflows/publish-repo.yml`, which runs
`jprm` (the Jellyfin Plugin Repository Manager) against `build.yaml` every time a `v*`
tag is pushed, and publishes/updates `manifest.json` + the built plugin zip on the
`gh-pages` branch. Pushing a new version tag is the entire release process from then on.

> **One manual, one-time step:** GitHub Pages itself has to be turned on for the
> `gh-pages` branch — that's a repo settings change this session's tooling isn't
> permitted to make for you. In the repo: **Settings → Pages → Source: Deploy from a
> branch → Branch: `gh-pages` / `/(root)` → Save.** After that the URL above goes live
> and stays live across every future release.

### Option B: manual install

1. Build the plugin (or grab `Jellyfin.Plugin.PlaylistMaker.dll` from a CI run's
   artifacts).
2. Copy it into a new folder under your Jellyfin server's plugin directory, e.g.
   `<jellyfin-data>/plugins/Playlist Maker/Jellyfin.Plugin.PlaylistMaker.dll`.
3. Restart Jellyfin.
4. Go to **Dashboard → Plugins → Playlist Maker**.

## Releasing a new version

Bump the `version` in `src/Jellyfin.Plugin.PlaylistMaker/build.yaml` (and add a
changelog entry there), commit, then tag and push:

```bash
git tag v1.0.1.0
git push origin v1.0.1.0
```

The `publish-repo` workflow builds that tag, packages it, and updates
`manifest.json` on `gh-pages`. Anyone who already added the repository URL in
Jellyfin sees the update in their catalog automatically — nothing else to do.

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
