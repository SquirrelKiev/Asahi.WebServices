using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Asahi.WebServices.ThemesProxy;

/// <summary>
/// Represents an S3 bucket.
/// </summary>
public class S3Storage : IStorage
{
    private readonly IAmazonS3 s3Client;
    private readonly S3StorageSettings s3StorageSettings;

    /// <summary>
    /// Creates an instance of <see cref="S3Storage"/>.
    /// </summary>
    public S3Storage(IAmazonS3 s3Client, IOptions<StorageSettings> storageConfig)
    {
        this.s3Client = s3Client;
        s3StorageSettings = storageConfig.Value.S3StorageSettings ??
                            throw new ArgumentNullException(nameof(IOptions<StorageSettings>.Value.S3StorageSettings));
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(s3StorageSettings.Bucket, path, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Uri> GetLinkToFileAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(s3StorageSettings.Bucket, path, cancellationToken);

            return new Uri(new Uri(s3StorageSettings.UserFacingBaseUrl), path);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException(path);
        }
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(string path, Stream stream, string contentType, long? length = null,
        CancellationToken cancellationToken = default)
    {
        var objReq = new PutObjectRequest()
        {
            BucketName = s3StorageSettings.Bucket,
            Key = path,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false
        };

        if (length is { } len)
        {
            objReq.Headers.ContentLength = len;
        }

        await s3Client.PutObjectAsync(objReq, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await s3Client.DeleteObjectAsync(s3StorageSettings.Bucket, path, cancellationToken);
    }
}