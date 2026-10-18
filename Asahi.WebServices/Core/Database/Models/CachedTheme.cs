using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Asahi.WebServices.Database.Models;

/// <summary>
/// A cached theme.
/// </summary>
[Index(nameof(LastRetrievedAt))]
public class CachedTheme
{
    /// <summary>
    /// The slug of the video that is cached.
    /// </summary>
    /// <example>Bakemonogatari-OP4</example>
    [Key, MaxLength(256)]
    public required string ThemeSlug { get; set; }

    /// <summary>
    /// The file size of the video in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// When the file was saved.
    /// </summary>
    public Instant SavedAt { get; set; }
    /// <summary>
    /// The last time the file was served.
    /// </summary>
    public Instant LastRetrievedAt { get; set; }
}