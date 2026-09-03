using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Request body for matching a batch of imported rows against the library.
/// </summary>
public class ImportMatchRequestDto
{
    /// <summary>
    /// Gets or sets the requesting user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the rows to match.
    /// </summary>
    public IReadOnlyList<ImportRowDto> Rows { get; set; } = Array.Empty<ImportRowDto>();
}
