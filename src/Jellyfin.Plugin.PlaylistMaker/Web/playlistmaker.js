(function () {
    'use strict';

    var pageEl = document.querySelector('#PlaylistMakerPage');
    if (!pageEl) {
        return;
    }

    var CHIP_LIMIT = 24;

    var state = {
        userId: null,
        draft: [],
        selectedGenres: new Set(),
        selectedArtists: new Set(),
        searchTimer: null,
        recommendTimer: null
    };

    function apiClient() {
        return window.ApiClient;
    }

    function buildQuery(params) {
        var usp = new URLSearchParams();
        Object.keys(params || {}).forEach(function (key) {
            var value = params[key];
            if (value === undefined || value === null || value === '') {
                return;
            }
            if (Array.isArray(value)) {
                value.forEach(function (v) { usp.append(key, v); });
            } else {
                usp.append(key, value);
            }
        });
        var qs = usp.toString();
        return qs ? ('?' + qs) : '';
    }

    function authHeader() {
        var token = apiClient().accessToken();
        return 'MediaBrowser Client="Playlist Maker", Token="' + token + '"';
    }

    function apiGet(path, params) {
        var url = apiClient().getUrl(path) + buildQuery(params);
        return fetch(url, { headers: { Authorization: authHeader() } })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Request failed (' + response.status + ')');
                }
                return response.status === 204 ? null : response.json();
            });
    }

    function apiPost(path, body) {
        var url = apiClient().getUrl(path);
        return fetch(url, {
            method: 'POST',
            headers: {
                Authorization: authHeader(),
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(body)
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('Request failed (' + response.status + ')');
            }
            return response.status === 204 ? null : response.json();
        });
    }

    function formatDuration(ticks) {
        if (!ticks) {
            return '';
        }
        var totalSeconds = Math.round(ticks / 10000000);
        var minutes = Math.floor(totalSeconds / 60);
        var seconds = totalSeconds % 60;
        return minutes + ':' + (seconds < 10 ? '0' : '') + seconds;
    }

    function clearChildren(el) {
        while (el.firstChild) {
            el.removeChild(el.firstChild);
        }
    }

    function trackMetaText(track) {
        var parts = [];
        if (track.artists && track.artists.length) {
            parts.push(track.artists.join(', '));
        }
        if (track.album) {
            parts.push(track.album);
        }
        var duration = formatDuration(track.runTimeTicks);
        if (duration) {
            parts.push(duration);
        }
        return parts.join(' • ');
    }

    function buildTrackRow(track, options) {
        var row = document.createElement('div');
        row.className = 'pmk-trackRow';

        var info = document.createElement('div');
        info.className = 'pmk-trackInfo';

        var name = document.createElement('div');
        name.className = 'pmk-trackName';
        name.textContent = track.name;
        info.appendChild(name);

        var meta = document.createElement('div');
        meta.className = 'pmk-trackMeta';
        meta.textContent = trackMetaText(track);
        info.appendChild(meta);

        if (track.matchReason) {
            var reason = document.createElement('div');
            reason.className = 'pmk-trackReason';
            reason.textContent = track.matchReason;
            info.appendChild(reason);
        }

        row.appendChild(info);

        var action = document.createElement('button');
        action.type = 'button';
        action.className = 'pmk-trackAction' + (options.remove ? ' pmk-removeAction' : '');
        action.textContent = options.remove ? '−' : '+';
        action.title = options.remove ? 'Remove from playlist' : 'Add to playlist';
        action.addEventListener('click', function () { options.onClick(track); });
        row.appendChild(action);

        return row;
    }

    function renderList(container, tracks, options) {
        clearChildren(container);
        if (!tracks || tracks.length === 0) {
            var empty = document.createElement('p');
            empty.className = 'pmk-empty';
            empty.textContent = options.emptyText;
            container.appendChild(empty);
            return;
        }
        tracks.forEach(function (track) {
            container.appendChild(buildTrackRow(track, options));
        });
    }

    function renderChips(container, label, items, selectedSet, onToggle) {
        clearChildren(container);
        var labelEl = document.createElement('span');
        labelEl.className = 'pmk-chipLabel';
        labelEl.textContent = label;
        container.appendChild(labelEl);

        items.slice(0, CHIP_LIMIT).forEach(function (item) {
            var chip = document.createElement('button');
            chip.type = 'button';
            chip.className = 'pmk-chip' + (selectedSet.has(item) ? ' pmk-chipActive' : '');
            chip.textContent = item;
            chip.addEventListener('click', function () {
                if (selectedSet.has(item)) {
                    selectedSet.delete(item);
                } else {
                    selectedSet.add(item);
                }
                chip.classList.toggle('pmk-chipActive');
                scheduleRecommendationRefresh(0);
            });
            container.appendChild(chip);
        });
    }

    function draftIds() {
        return state.draft.map(function (t) { return t.id; });
    }

    function renderDraft() {
        var listEl = pageEl.querySelector('#pmkDraftList');
        var summaryEl = pageEl.querySelector('#pmkDraftSummary');
        var saveButton = pageEl.querySelector('#pmkSaveButton');

        renderList(listEl, state.draft, {
            remove: true,
            emptyText: 'Add tracks from Discover or Recommended to build your playlist.',
            onClick: function (track) { removeFromDraft(track.id); }
        });

        var totalTicks = state.draft.reduce(function (sum, t) { return sum + (t.runTimeTicks || 0); }, 0);
        var totalMinutes = Math.round(totalTicks / 10000000 / 60);
        summaryEl.textContent = state.draft.length + (state.draft.length === 1 ? ' track' : ' tracks') +
            (totalMinutes > 0 ? ' • ~' + totalMinutes + ' min' : '');

        saveButton.disabled = state.draft.length === 0;
    }

    function addToDraft(track) {
        if (state.draft.some(function (t) { return t.id === track.id; })) {
            return;
        }
        state.draft.push(track);
        renderDraft();
        scheduleRecommendationRefresh(0);
    }

    function removeFromDraft(id) {
        state.draft = state.draft.filter(function (t) { return t.id !== id; });
        renderDraft();
        scheduleRecommendationRefresh(0);
    }

    function showStatus(message, isError) {
        var statusEl = pageEl.querySelector('#pmkStatus');
        statusEl.textContent = message;
        statusEl.className = 'pmk-status ' + (isError ? 'pmk-statusError' : 'pmk-statusOk');
    }

    function runSearch(query) {
        var resultsEl = pageEl.querySelector('#pmkSearchResults');
        if (!query) {
            renderList(resultsEl, [], { emptyText: 'Search above, or pick a genre / artist to get started.' });
            return;
        }
        apiGet('PlaylistMaker/Search', { userId: state.userId, query: query, limit: 30 })
            .then(function (tracks) {
                renderList(resultsEl, tracks, {
                    remove: false,
                    emptyText: 'No matches found.',
                    onClick: addToDraft
                });
            })
            .catch(function (err) {
                renderList(resultsEl, [], { emptyText: 'Search failed: ' + err.message });
            });
    }

    function scheduleRecommendationRefresh(delay) {
        window.clearTimeout(state.recommendTimer);
        state.recommendTimer = window.setTimeout(refreshRecommendations, delay === undefined ? 250 : delay);
    }

    function refreshRecommendations() {
        var resultsEl = pageEl.querySelector('#pmkRecommendResults');
        var hintEl = pageEl.querySelector('#pmkRecommendHint');

        var hasSeed = state.draft.length > 0 || state.selectedGenres.size > 0 || state.selectedArtists.size > 0;
        hintEl.textContent = hasSeed
            ? 'Based on the tracks and genres/artists you’ve picked.'
            : 'Popular in your library. Add a track or pick a genre/artist to personalize these.';

        apiGet('PlaylistMaker/Recommendations', {
            userId: state.userId,
            seedItemIds: draftIds(),
            seedGenres: Array.from(state.selectedGenres),
            seedArtists: Array.from(state.selectedArtists),
            excludeItemIds: draftIds(),
            limit: 20
        }).then(function (tracks) {
            renderList(resultsEl, tracks, {
                remove: false,
                emptyText: 'No recommendations yet — try adding a track or picking a genre/artist.',
                onClick: addToDraft
            });
        }).catch(function (err) {
            renderList(resultsEl, [], { emptyText: 'Could not load recommendations: ' + err.message });
        });
    }

    function loadFacets() {
        apiGet('PlaylistMaker/Genres', { userId: state.userId }).then(function (genres) {
            renderChips(pageEl.querySelector('#pmkGenreChips'), 'Genres', genres || [], state.selectedGenres, scheduleRecommendationRefresh);
        }).catch(function () { /* non-fatal */ });

        apiGet('PlaylistMaker/Artists', { userId: state.userId }).then(function (artists) {
            renderChips(pageEl.querySelector('#pmkArtistChips'), 'Artists', artists || [], state.selectedArtists, scheduleRecommendationRefresh);
        }).catch(function () { /* non-fatal */ });
    }

    function savePlaylist() {
        var nameInput = pageEl.querySelector('#pmkPlaylistName');
        var publicCheckbox = pageEl.querySelector('#pmkPlaylistPublic');
        var saveButton = pageEl.querySelector('#pmkSaveButton');
        var name = nameInput.value.trim();

        if (!name) {
            showStatus('Give your playlist a name first.', true);
            return;
        }
        if (state.draft.length === 0) {
            showStatus('Add at least one track first.', true);
            return;
        }

        saveButton.disabled = true;
        apiPost('PlaylistMaker/Playlists', {
            name: name,
            userId: state.userId,
            itemIds: draftIds(),
            public: !!publicCheckbox.checked
        }).then(function () {
            showStatus('Saved "' + name + '" with ' + state.draft.length + ' tracks.', false);
        }).catch(function (err) {
            showStatus('Could not save playlist: ' + err.message, true);
        }).finally(function () {
            saveButton.disabled = state.draft.length === 0;
        });
    }

    function init() {
        var client = apiClient();
        if (!client) {
            return;
        }
        state.userId = client.getCurrentUserId();
        state.draft = [];
        state.selectedGenres = new Set();
        state.selectedArtists = new Set();

        renderDraft();
        loadFacets();
        refreshRecommendations();

        var searchInput = pageEl.querySelector('#pmkSearchInput');
        searchInput.value = '';
        searchInput.addEventListener('input', function () {
            var value = searchInput.value.trim();
            window.clearTimeout(state.searchTimer);
            state.searchTimer = window.setTimeout(function () { runSearch(value); }, 300);
        });

        pageEl.querySelector('#pmkSaveButton').addEventListener('click', savePlaylist);
        pageEl.querySelector('#pmkShuffleButton').addEventListener('click', function () { refreshRecommendations(); });
    }

    pageEl.addEventListener('pageshow', init);
}());
