using System.IO;
using System.Threading.Tasks;
using minio_csharpClient.Models;

namespace minio_csharpClient.Services;

public interface IMinioService
{
    Task<bool> PingAsync();
    Task<List<BucketViewModel>> GetBucketsAsync();
    Task<bool> CreateBucketAsync(string bucketName);
    Task<bool> DeleteBucketAsync(string bucketName);
    Task<BucketContentViewModel> GetBucketContentAsync(string bucketName, string? prefix = null);
    Task UploadObjectAsync(string bucketName, string objectKey, Stream stream, long size, string contentType);
    Task CreateFolderAsync(string bucketName, string currentPrefix, string folderName);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadObjectAsync(string bucketName, string objectKey);
    Task DeleteObjectAsync(string bucketName, string objectKey);
    Task DeleteFolderAsync(string bucketName, string folderPrefix);
    Task<string> GetPresignedUrlAsync(string bucketName, string objectKey, int expirySeconds = 86400);
    Task<string> GetObjectTextContentAsync(string bucketName, string objectKey, int maxBytes = 1048576);
}
