using System;

namespace minio_csharpClient.Models;

public class BucketViewModel
{
    public string Name { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string FormattedCreationDate => CreationDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    public int ObjectCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string FormattedTotalSize => FormatBytes(TotalSizeBytes);

    public static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}

public class CreateBucketRequest
{
    public string BucketName { get; set; } = string.Empty;
}
