using System;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <summary>
/// Limits how many music requests a user can make within a rolling time window, per the admin's
/// configured <c>MaxRequestsPerUser</c> / <c>RequestWindowHours</c> settings.
/// </summary>
public interface IRequestRateLimiter
{
    /// <summary>
    /// Records a request for the given user if they're under their limit.
    /// </summary>
    /// <param name="userId">The requesting user's id.</param>
    /// <param name="retryAfter">How long until the user's oldest request in the window expires, if rate limited.</param>
    /// <returns><see langword="true"/> if the request was recorded and is allowed to proceed.</returns>
    bool TryRecordRequest(Guid userId, out TimeSpan retryAfter);
}
