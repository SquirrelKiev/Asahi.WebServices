namespace Asahi.WebServices.ThemesProxy;

/// <summary>
/// Represents a place to put files and read files.
/// </summary>
public interface IStorage
{
    /// <summary>
    /// Does a file exist at <paramref name="path"/>?
    /// </summary>
    /// <param name="path">The path in question.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the file exists.</returns>
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a URL to a file from storage.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A URL to the file.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file at the path does not exist.</exception>
    public Task<Uri> GetLinkToFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the contents of the stream to a file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="stream">The stream to write to the file.</param>
    /// <param name="contentType">The MIME type of the file.</param>
    /// <param name="length">The length of the file. Use if the stream does not provide a length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task WriteFileAsync(string path, Stream stream, string contentType, long? length = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes the file at <paramref name="path"/>, if it exists.
    /// </summary>
    /// <param name="path">The path of the file to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);
}