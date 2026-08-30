# Jellyfin Playlist Maker

A Jellyfin server plugin with a standalone **Playlist Maker** app — a fast,
Spotify/Apple-Music-style playlist builder for your own music library, with a live
"Recommended For You" panel driven by genre and artist matching. Any user on your
server can sign in and use it, not just admins.

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
plugin folder), not a modified web client — but it deliberately does *not* rely on
Jellyfin's usual plugin-page mechanism (`Dashboard → Plugins → <name>`) as the way
users reach it, because that mechanism can't do the job:

Jellyfin's web client wraps its *entire* `/dashboard` section — where every plugin's
config page lives, no exceptions — in a hard admin-only route guard
(`ConnectionRequired level='admin'` in `jellyfin-web`'s source). A non-admin user who
navigates there gets bounced away before the page even loads. There's also no
`EnableInMainMenu`-style flag or any other stock mechanism that places a plugin page
somewhere a regular user can reach; that's confirmed directly from the `jellyfin-web`
source, not assumed.

So the plugin instead serves its **own standalone page**, entirely outside
`jellyfin-web`, at:

```
http://<your-server>:8096/PlaylistMaker/App
```

This page has its own small login screen (username/password, same credentials as
normal), authenticating directly against Jellyfin's own `/Users/AuthenticateByName`
endpoint — the same one every official Jellyfin client uses. Once signed in, the token
is kept in the browser's local storage so it stays signed in on return visits. Because
this page is served straight from the plugin's own controller rather than through
`jellyfin-web`'s router, the admin-only gate never applies to it — any user with a
Jellyfin account can use it.

A `Dashboard → Plugins → Playlist Maker` page still exists too, mainly for the
Configuration settings below (which only admins should be tuning anyway) — but the
standalone `/PlaylistMaker/App` page is what you'd actually hand out to your users.

Note: this assumes Jellyfin is served from the root of its domain/IP (the default for
most setups). If you run Jellyfin behind a reverse proxy with a custom base path
(`--baseurl`), the root-relative API calls this page makes (`/PlaylistMaker/...`,
`/Users/AuthenticateByName`) would need that base path prepended — not handled yet.

## Project layout

```
src/Jellyfin.Plugin.PlaylistMaker/
  Plugin.cs                        Plugin entry point, registers the Dashboard config page
  PluginServiceRegistrator.cs      DI registration for the recommendation service
  Configuration/
    PluginConfiguration.cs         Tunable weights (see below)
  Api/
    PlaylistMakerController.cs     REST endpoints, plus GET /PlaylistMaker/App (the standalone page)
    Dto/                           Request/response models
  Services/
    IRecommendationService.cs
    RecommendationService.cs       Genre/artist scoring + cold-start fallback
  Web/
    app.html                       The standalone app: login screen + builder UI, any user
    playlistmaker.html             The Dashboard-only admin config page
  build.yaml / meta.json           Plugin manifest metadata
```

## Building

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
dotnet build src/Jellyfin.Plugin.PlaylistMaker/Jellyfin.Plugin.PlaylistMaker.csproj -c Release
```

The build targets `Jellyfin.Controller` `10.11.*` on `net9.0`, matching the Jellyfin
10.11.x server line (Jellyfin moved its host process from .NET 8 to .NET 9 as of 10.11.0).
If your server runs a different major Jellyfin version, update both the `TargetFramework`
and the `PackageReference` version in the `.csproj` to match — a mismatch here doesn't
just fail to compile, it can load fine and then throw `TypeLoadException`/
`MissingMethodException` at runtime, since a plugin built for one host runtime is not
binary-compatible with another. Also adjust `targetAbi`/`framework` in `build.yaml` /
`meta.json` to match.

A GitHub Actions workflow (`.github/workflows/build.yml`) builds the plugin on every
push, so you can check the Actions tab for compile status without needing a local
.NET install.

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
4. Send your users to `http://<your-server>:8096/PlaylistMaker/App` — that's the app,
   with its own login. (Admins can additionally find a settings page under
   Dashboard → Plugins → Playlist Maker.)

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
4. Send your users to `http://<your-server>:8096/PlaylistMaker/App`.

## Optional: a quick-access button in the Jellyfin web UI

Since `/PlaylistMaker/App` is a separate page outside `jellyfin-web`, there's nothing
in the stock Jellyfin UI that links to it. **This entire section is optional** —
Playlist Maker itself needs none of this; it's purely a convenience for anyone who
already has a way to inject custom CSS/JS into the Jellyfin web client. The built-in
**Dashboard → General → Custom CSS** field covers the CSS half. The JS half needs
something outside Jellyfin itself, since there's no built-in field for it — a
community "JavaScript Injector"-style plugin is a straightforward option if you'd
rather not touch server files (search your plugin repositories for one; not something
this project bundles or endorses a specific fork of); a `<script>` tag added directly
to `jellyfin-web`'s `index.html`, or a reverse-proxy rule (nginx `sub_filter`, Caddy
`replace`, etc.) work just as well.

The snippet below adds a floating "Playlist Maker" button, shown while you're browsing
a music library's tabs (Albums, Suggestions, Album artists, Artists, Playlists, Songs,
Genres — detected by tab label, so it follows you across whichever one is active
instead of only appearing on Playlists). Clicking it opens the app in a small
draggable, resizable panel docked over the page — a mini browser rather than a second
tab — with its own "open in a new tab" and close buttons in the panel header.

Replace `PLAYLIST_MAKER_URL` with your own server's address before using it.

```css
#jfPlaylistMakerButton {
    position: fixed;
    right: 20px;
    bottom: 20px;
    z-index: 1000;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 0.5em;
    min-height: 40px;
    padding: 0.65em 1.15em;
    border: 0;
    border-radius: 999px;
    background: #00A4DC;
    color: #fff;
    font-size: 1rem;
    font-weight: 500;
    line-height: 1;
    cursor: pointer;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.35);
    transition: background-color 0.2s ease, transform 0.2s ease, box-shadow 0.2s ease;
}

#jfPlaylistMakerButton:hover {
    background: #0090c2;
    transform: translateY(-1px);
    box-shadow: 0 6px 16px rgba(0, 0, 0, 0.4);
}

#jfPlaylistMakerButton:active {
    transform: translateY(0);
}

#jfPlaylistMakerButton:focus-visible {
    outline: 2px solid #fff;
    outline-offset: 2px;
}

#jfPlaylistMakerButton .playlistMakerIcon {
    font-family: "Material Icons";
    font-size: 1.4em;
    line-height: 1;
}

#jfPlaylistMakerPanel {
    position: fixed;
    right: 20px;
    bottom: 76px;
    z-index: 1001;
    width: 420px;
    height: 640px;
    max-width: calc(100vw - 40px);
    max-height: calc(100vh - 100px);
    min-width: 280px;
    min-height: 300px;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    background: #1a1a1a;
    border: 1px solid rgba(255, 255, 255, 0.15);
    border-radius: 10px;
    box-shadow: 0 12px 40px rgba(0, 0, 0, 0.5);
    resize: both;
}

#jfPlaylistMakerPanel[hidden] {
    display: none;
}

#jfPlaylistMakerPanelHeader {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-shrink: 0;
    padding: 0.5em 0.4em 0.5em 0.9em;
    background: rgba(255, 255, 255, 0.06);
    color: #fff;
    font-size: 0.9em;
    font-weight: 600;
    cursor: move;
    user-select: none;
}

#jfPlaylistMakerPanelActions {
    display: flex;
    gap: 0.2em;
}

#jfPlaylistMakerPanelActions button {
    padding: 0.35em 0.5em;
    border: 0;
    border-radius: 5px;
    background: transparent;
    color: #ccc;
    font-size: 1em;
    line-height: 1;
    cursor: pointer;
}

#jfPlaylistMakerPanelActions button:hover {
    background: rgba(255, 255, 255, 0.12);
    color: #fff;
}

#jfPlaylistMakerPanel iframe {
    flex: 1;
    width: 100%;
    border: 0;
    background: #0d0d0d;
}
```

```js
(() => {
    "use strict";

    // Point this at your own server - the standalone app's URL.
    const PLAYLIST_MAKER_URL = "https://your-jellyfin-server.example.com/PlaylistMaker/App";

    const BUTTON_ID = "jfPlaylistMakerButton";
    const PANEL_ID = "jfPlaylistMakerPanel";

    // Tab labels from a music library's own tab strip (Albums / Suggestions / Album
    // artists / Artists / Playlists / Songs / Genres). Matched by exact rendered text
    // rather than a specific CSS class or URL pattern, so it keeps working across
    // Jellyfin versions/skins and follows you across whichever tab is active.
    const LIBRARY_TAB_LABELS = new Set([
        "Albums", "Suggestions", "Album artists", "Artists", "Playlists", "Songs", "Genres"
    ]);

    function isOnMusicLibraryTab() {
        const clickable = document.querySelectorAll("button, a");
        for (const el of clickable) {
            if (LIBRARY_TAB_LABELS.has((el.textContent || "").trim())) {
                return true;
            }
        }
        return false;
    }

    function createButton() {
        const button = document.createElement("button");
        button.type = "button";
        button.id = BUTTON_ID;
        button.setAttribute("aria-label", "Open Playlist Maker");
        button.innerHTML = `
            <span class="playlistMakerIcon material-icons" aria-hidden="true">playlist_add</span>
            <span>Playlist Maker</span>
        `;
        button.addEventListener("click", togglePanel);
        return button;
    }

    function makeDraggable(panel, handle) {
        let dragging = false;
        let startX = 0;
        let startY = 0;
        let startLeft = 0;
        let startTop = 0;

        handle.addEventListener("mousedown", (e) => {
            if (e.target.closest("button")) {
                return;
            }
            dragging = true;
            const rect = panel.getBoundingClientRect();
            startX = e.clientX;
            startY = e.clientY;
            startLeft = rect.left;
            startTop = rect.top;
            panel.style.right = "auto";
            panel.style.bottom = "auto";
            e.preventDefault();
        });

        window.addEventListener("mousemove", (e) => {
            if (!dragging) {
                return;
            }
            panel.style.left = Math.max(0, startLeft + (e.clientX - startX)) + "px";
            panel.style.top = Math.max(0, startTop + (e.clientY - startY)) + "px";
        });

        window.addEventListener("mouseup", () => {
            dragging = false;
        });
    }

    let panel = null;

    function ensurePanel() {
        if (panel) {
            return panel;
        }

        panel = document.createElement("div");
        panel.id = PANEL_ID;
        panel.hidden = true;

        const header = document.createElement("div");
        header.id = "jfPlaylistMakerPanelHeader";
        header.innerHTML = `<span>Playlist Maker</span>`;

        const actions = document.createElement("div");
        actions.id = "jfPlaylistMakerPanelActions";

        const openTab = document.createElement("button");
        openTab.type = "button";
        openTab.title = "Open in a new tab";
        openTab.textContent = "⤢";
        openTab.addEventListener("click", () => window.open(PLAYLIST_MAKER_URL, "_blank", "noopener,noreferrer"));

        const close = document.createElement("button");
        close.type = "button";
        close.title = "Close";
        close.textContent = "✕";
        close.addEventListener("click", () => { panel.hidden = true; });

        actions.append(openTab, close);
        header.appendChild(actions);
        panel.appendChild(header);

        const iframe = document.createElement("iframe");
        iframe.src = PLAYLIST_MAKER_URL;
        iframe.title = "Playlist Maker";
        panel.appendChild(iframe);

        makeDraggable(panel, header);
        document.body.appendChild(panel);
        return panel;
    }

    function togglePanel() {
        const p = ensurePanel();
        p.hidden = !p.hidden;
    }

    function update() {
        const shouldShow = isOnMusicLibraryTab();
        let button = document.getElementById(BUTTON_ID);

        if (shouldShow && !button) {
            button = createButton();
            document.body.appendChild(button);
        } else if (!shouldShow && button) {
            button.remove();
        }
    }

    window.addEventListener("hashchange", update);
    window.addEventListener("popstate", update);
    document.addEventListener("DOMContentLoaded", update);

    // Jellyfin's web client is a single-page app - it swaps page content in place
    // without a full reload, so hashchange/popstate alone miss most in-app
    // navigation (including switching between tabs within the same library view).
    // This watches for that DOM churn and re-evaluates whether the button should
    // show. The panel itself (once opened) is appended straight to <body> and is
    // left alone here - it stays open across navigation until you close it.
    new MutationObserver(update).observe(document.documentElement, {
        childList: true,
        subtree: true
    });

    update();
})();
```

## Releasing a new version

Bump the `version` in `src/Jellyfin.Plugin.PlaylistMaker/build.yaml` (and add a
changelog entry there), commit, and push to the branch — that's the whole
process now.

The `auto-tag` workflow watches for `build.yaml` changing on push and tags the
commit (`vX.Y.Z.W`, matching the new version) automatically. That tag push
triggers `publish-repo`, which builds it, packages it, and updates
`manifest.json` on `gh-pages`. Anyone who already added the repository URL in
Jellyfin sees the update in their catalog automatically. No manual
`git tag`/`git push origin <tag>` step, and nothing machine-specific — a plain
`git push` of the branch from anywhere is enough.

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
(same as any other Jellyfin API call) — except `GET /PlaylistMaker/App`, which is
public since it's the login page itself:

| Endpoint | Purpose |
|---|---|
| `GET /PlaylistMaker/App` | The standalone app (login screen + builder), open to any user |
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
