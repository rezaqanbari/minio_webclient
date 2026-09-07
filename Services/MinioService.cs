using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using minio_csharpClient.Models;

namespace minio_csharpClient.Services;

public class MinioService : IMinioService
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioService> _logger;

    public MinioService(IOptions<MinioOptions> options, ILogger<MinioService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var builder = new MinioClient()
            .WithEndpoint(_options.CleanEndpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (_options.WithSSL)
        {
            builder = builder.WithSSL();
        }

        var region = !string.IsNullOrWhiteSpace(_options.Region) ? _options.Region : "us-east-1";
        builder = builder.WithRegion(region);

        _client = builder.Build();
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            await _client.ListBucketsAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MinIO server.");
            return false;
        }
    }

    public async Task<List<BucketViewModel>> GetBucketsAsync()
    {
        try
        {
            var response = await _client.ListBucketsAsync();
            var list = new List<BucketViewModel>();

            foreach (var b in response.Buckets)
            {
                list.Add(new BucketViewModel
                {
                    Name = b.Name,
                    CreationDate = b.CreationDateDateTime
                });
            }

            return list.OrderBy(x => x.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting buckets list.");
            throw;
        }
    }

    public async Task<bool> CreateBucketAsync(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("نام باکت نمی‌تواند خالی باشد.", nameof(bucketName));

        var cleanName = bucketName.Trim().ToLowerInvariant();

        var makeArgs = new MakeBucketArgs().WithBucket(cleanName);
        if (!string.IsNullOrWhiteSpace(_options.Region))
        {
            makeArgs = makeArgs.WithLocation(_options.Region);
        }

        try
        {
            await _client.MakeBucketAsync(makeArgs);
            _logger.LogInformation("Bucket {BucketName} created successfully.", cleanName);
            return true;
        }
        catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("AlreadyOwnedByYou", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"باکت با نام '{cleanName}' از قبل وجود دارد.");
        }
    }

    public async Task<bool> DeleteBucketAsync(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("نام باکت مشخص نشده است.", nameof(bucketName));

        var cleanName = bucketName.Trim();

        var removeArgs = new RemoveBucketArgs().WithBucket(cleanName);
        await _client.RemoveBucketAsync(removeArgs);
        _logger.LogInformation("Bucket {BucketName} deleted successfully.", cleanName);
        return true;
    }

    public async Task<BucketContentViewModel> GetBucketContentAsync(string bucketName, string? prefix = null)
    {
        prefix ??= string.Empty;
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith('/'))
        {
            prefix += "/";
        }

        var result = new BucketContentViewModel
        {
            BucketName = bucketName,
            CurrentPrefix = prefix,
            ParentPrefix = GetParentPrefix(prefix),
            Breadcrumbs = BuildBreadcrumbs(bucketName, prefix),
            Items = new List<ObjectItemViewModel>()
        };

        var listArgs = new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithPrefix(prefix)
            .WithRecursive(false);

        var listEnum = _client.ListObjectsEnumAsync(listArgs);

        await foreach (var item in listEnum)
        {
            // Skip the current directory object itself if MinIO returned it
            if (item.Key == prefix)
                continue;

            string displayName = item.Key;
            if (!string.IsNullOrEmpty(prefix) && displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                displayName = displayName.Substring(prefix.Length);
            }

            bool isDir = item.IsDir || item.Key.EndsWith('/');
            displayName = displayName.TrimEnd('/');

            if (string.IsNullOrEmpty(displayName))
                continue;

            result.Items.Add(new ObjectItemViewModel
            {
                BucketName = bucketName,
                Key = item.Key,
                DisplayName = displayName,
                IsDir = isDir,
                Size = (long)item.Size,
                LastModified = item.LastModifiedDateTime,
                ETag = item.ETag
            });
        }

        // Order directories first, then files alphabetically
        result.Items = result.Items
            .OrderByDescending(x => x.IsDir)
            .ThenBy(x => x.DisplayName)
            .ToList();

        return result;
    }

    public async Task UploadObjectAsync(string bucketName, string objectKey, Stream stream, long size, string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = "application/octet-stream";
        }

        var putArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType);

        await _client.PutObjectAsync(putArgs);
        _logger.LogInformation("Object {ObjectKey} uploaded to bucket {BucketName}.", objectKey, bucketName);
    }

    public async Task CreateFolderAsync(string bucketName, string currentPrefix, string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            throw new ArgumentException("نام پوشه نمی‌تواند خالی باشد.", nameof(folderName));

        currentPrefix ??= string.Empty;
        if (!string.IsNullOrEmpty(currentPrefix) && !currentPrefix.EndsWith('/'))
        {
            currentPrefix += "/";
        }

        var cleanFolderName = folderName.Trim().Trim('/').Replace("\\", "/");
        var folderKey = $"{currentPrefix}{cleanFolderName}/";

        using var emptyStream = new MemoryStream(Array.Empty<byte>());
        var putArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(folderKey)
            .WithStreamData(emptyStream)
            .WithObjectSize(0)
            .WithContentType("application/x-directory");

        await _client.PutObjectAsync(putArgs);
        _logger.LogInformation("Folder {FolderKey} created in bucket {BucketName}.", folderKey, bucketName);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadObjectAsync(string bucketName, string objectKey)
    {
        var memoryStream = new MemoryStream();

        var getArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
            });

        await _client.GetObjectAsync(getArgs);
        memoryStream.Position = 0;

        string fileName = Path.GetFileName(objectKey.TrimEnd('/'));
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "downloaded_file";
        }

        string contentType = minio_csharpClient.Controllers.ObjectsController.GetExactMimeType(fileName);

        return (memoryStream, contentType, fileName);
    }

    public async Task DeleteObjectAsync(string bucketName, string objectKey)
    {
        var rmArgs = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey);

        await _client.RemoveObjectAsync(rmArgs);
        _logger.LogInformation("Object {ObjectKey} deleted from bucket {BucketName}.", objectKey, bucketName);
    }

    public async Task DeleteFolderAsync(string bucketName, string folderPrefix)
    {
        if (!folderPrefix.EndsWith('/'))
        {
            folderPrefix += "/";
        }

        var listArgs = new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithPrefix(folderPrefix)
            .WithRecursive(true);

        var items = _client.ListObjectsEnumAsync(listArgs);
        await foreach (var item in items)
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucketName).WithObject(item.Key));
        }

        // Also remove folder placeholder itself if present
        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucketName).WithObject(folderPrefix));
        }
        catch
        {
            // Ignored if it doesn't exist
        }

        _logger.LogInformation("Folder {FolderPrefix} and all its contents deleted from bucket {BucketName}.", folderPrefix, bucketName);
    }

    public async Task<string> GetPresignedUrlAsync(string bucketName, string objectKey, int expirySeconds = 86400)
    {
        if (expirySeconds <= 0 || expirySeconds > 7 * 24 * 3600)
        {
            expirySeconds = 86400; // default 24 hours
        }

        var presignedArgs = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry(expirySeconds);

        return await _client.PresignedGetObjectAsync(presignedArgs);
    }

    public async Task<string> GetObjectTextContentAsync(string bucketName, string objectKey, int maxBytes = 1048576)
    {
        using var memoryStream = new MemoryStream();
        var getArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithCallbackStream(stream =>
            {
                byte[] buffer = new byte[8192];
                int totalRead = 0;
                int read;
                while ((read = stream.Read(buffer, 0, Math.Min(buffer.Length, maxBytes - totalRead))) > 0)
                {
                    memoryStream.Write(buffer, 0, read);
                    totalRead += read;
                    if (totalRead >= maxBytes) break;
                }
            });

        await _client.GetObjectAsync(getArgs);
        memoryStream.Position = 0;

        using var reader = new StreamReader(memoryStream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public async Task<List<ObjectItemViewModel>> SearchObjectsAcrossBucketsAsync(IEnumerable<string> bucketNames, string query, string? scopedPrefix = null)
    {
        var results = new List<ObjectItemViewModel>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        query = query.Trim();

        foreach (var bucketName in bucketNames)
        {
            try
            {
                var listArgs = new ListObjectsArgs()
                    .WithBucket(bucketName)
                    .WithRecursive(true);

                if (!string.IsNullOrWhiteSpace(scopedPrefix))
                {
                    var cleanPrefix = scopedPrefix.Trim().TrimStart('/');
                    if (!cleanPrefix.EndsWith('/')) cleanPrefix += "/";
                    listArgs = listArgs.WithPrefix(cleanPrefix);
                }

                var listEnum = _client.ListObjectsEnumAsync(listArgs);
                await foreach (var item in listEnum)
                {
                    if (item.Key.EndsWith('/')) continue;

                    var fileName = Path.GetFileName(item.Key);
                    if (item.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        fileName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new ObjectItemViewModel
                        {
                            BucketName = bucketName,
                            Key = item.Key,
                            DisplayName = item.Key,
                            IsDir = false,
                            Size = (long)item.Size,
                            LastModified = item.LastModifiedDateTime,
                            ETag = item.ETag
                        });

                        if (results.Count >= 500) break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search in bucket {BucketName}", bucketName);
            }

            if (results.Count >= 500) break;
        }

        return results.OrderBy(x => x.BucketName).ThenBy(x => x.Key).ToList();
    }

    private static string? GetParentPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return null;

        var parts = prefix.TrimEnd('/').Split('/');
        if (parts.Length <= 1) return string.Empty;

        return string.Join('/', parts.Take(parts.Length - 1)) + "/";
    }

    private static List<BreadcrumbItem> BuildBreadcrumbs(string bucketName, string prefix)
    {
        var breadcrumbs = new List<BreadcrumbItem>
        {
            new() { Title = bucketName, Prefix = string.Empty, IsActive = string.IsNullOrEmpty(prefix) }
        };

        if (string.IsNullOrEmpty(prefix)) return breadcrumbs;

        var parts = prefix.TrimEnd('/').Split('/');
        var accumulated = "";

        for (int i = 0; i < parts.Length; i++)
        {
            accumulated += parts[i] + "/";
            bool isLast = (i == parts.Length - 1);
            breadcrumbs.Add(new BreadcrumbItem
            {
                Title = parts[i],
                Prefix = accumulated,
                IsActive = isLast
            });
        }

        return breadcrumbs;
    }
}
