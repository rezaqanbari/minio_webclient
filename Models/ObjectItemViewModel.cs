using System;
using System.IO;

namespace minio_csharpClient.Models;

public class ObjectItemViewModel
{
    public string BucketName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDir { get; set; }
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
    public string? ContentType { get; set; }
    public string? ETag { get; set; }

    public string FormattedSize => IsDir ? "-" : BucketViewModel.FormatBytes(Size);
    public string FormattedLastModified => LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public string Extension => IsDir ? "" : Path.GetExtension(Key).ToLowerInvariant();

    public string IconClass
    {
        get
        {
            if (IsDir) return "bi-folder-fill text-warning";
            return Extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".bmp" => "bi-file-earmark-image-fill text-primary",
                ".mp4" or ".webm" or ".mkv" or ".avi" or ".mov" or ".ts" or ".m3u8" => "bi-file-earmark-play-fill text-danger",
                ".mp3" or ".wav" or ".ogg" or ".aac" or ".flac" or ".m4a" => "bi-file-earmark-music-fill text-info",
                ".pdf" => "bi-file-earmark-pdf-fill text-danger",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "bi-file-earmark-zip-fill text-secondary",
                ".txt" or ".md" or ".json" or ".xml" or ".csv" or ".log" => "bi-file-earmark-text-fill text-success",
                ".doc" or ".docx" => "bi-file-earmark-word-fill text-primary",
                ".xls" or ".xlsx" => "bi-file-earmark-excel-fill text-success",
                _ => "bi-file-earmark-fill text-muted"
            };
        }
    }

    public string PreviewType
    {
        get
        {
            if (IsDir) return "none";
            return Extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" => "image",
                ".mp4" or ".webm" or ".mov" => "video",
                ".mp3" or ".wav" or ".ogg" or ".aac" => "audio",
                ".pdf" => "pdf",
                ".txt" or ".json" or ".xml" or ".log" or ".md" or ".csv" => "text",
                _ => "none"
            };
        }
    }

    public bool IsPreviewable => PreviewType != "none";
}

public class BreadcrumbItem
{
    public string Title { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class BucketContentViewModel
{
    public string BucketName { get; set; } = string.Empty;
    public string CurrentPrefix { get; set; } = string.Empty;
    public string? ParentPrefix { get; set; }
    public List<BreadcrumbItem> Breadcrumbs { get; set; } = new();
    public List<ObjectItemViewModel> Items { get; set; } = new();
    public int TotalDirectories => Items.Count(x => x.IsDir);
    public int TotalFiles => Items.Count(x => !x.IsDir);
    public long TotalFilesSize => Items.Where(x => !x.IsDir).Sum(x => x.Size);
    public string FormattedTotalFilesSize => BucketViewModel.FormatBytes(TotalFilesSize);
}

public class PresignedUrlRequest
{
    public string BucketName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 24;
}

public class CreateFolderRequest
{
    public string BucketName { get; set; } = string.Empty;
    public string CurrentPrefix { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
}
