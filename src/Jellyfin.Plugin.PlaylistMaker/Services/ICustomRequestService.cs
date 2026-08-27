using System;
using System.Collections.Generic;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <summary>
/// Stores user-submitted "custom requests" - a link (and optional note) for something outside
/// what the Lidarr artist/album search can resolve on its own - for an admin to review and
/// action manually from the settings page.
/// </summary>
public interface ICustomRequestService
{
    /// <summary>
    /// Records a new custom request.
    /// </summary>
    /// <param name="userName">Display name of the submitting user.</param>
    /// <param name="link">The link describing what's being requested.</param>
    /// <param name="note">An optional note with extra context.</param>
    /// <returns>The stored request record.</returns>
    CustomRequestRecordDto Add(string userName, string link, string? note);

    /// <summary>
    /// Gets every stored custom request, newest first.
    /// </summary>
    /// <returns>All stored requests.</returns>
    IReadOnlyList<CustomRequestRecordDto> GetAll();

    /// <summary>
    /// Removes a stored custom request, e.g. once an admin has handled it.
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <returns><see langword="true"/> if a request was removed.</returns>
    bool Remove(Guid id);
}
