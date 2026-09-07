using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

namespace minio_csharpClient.Controllers;

public class ObjectsController : Controller
{
    private readonly IMinioService _minioService;

    public ObjectsController(IMinioService minioService)
    {
        _minioService = minioService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string bucketName, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            TempData["Error"] = "نام باکت مشخص نشده است.";
            return RedirectToAction("Index", "Buckets");
        }

        try
        {
            var model = await _minioService.GetBucketContentAsync(bucketName, prefix);
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در بارگذاری محتوای باکت: {ex.Message}";
            return RedirectToAction("Index", "Buckets");
        }
    }

    [HttpPost]
    [RequestSizeLimit(524288000)] // 500 MB limit
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
    public async Task<IActionResult> Upload(string bucketName, string? prefix, List<IFormFile> files)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            TempData["Error"] = "نام باکت مشخص نشده است.";
            return RedirectToAction("Index", "Buckets");
        }

        if (files == null || files.Count == 0)
        {
            TempData["Error"] = "هیچ فایلی برای آپلود انتخاب نشده است.";
            return RedirectToAction(nameof(Index), new { bucketName, prefix });
        }

        prefix ??= string.Empty;
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith('/'))
        {
            prefix += "/";
        }

        int successCount = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            string fileName = Path.GetFileName(file.FileName);
            string objectKey = $"{prefix}{fileName}";

            try
            {
                using var stream = file.OpenReadStream();
                var detectedContentType = GetExactMimeType(fileName, file.ContentType);
                await _minioService.UploadObjectAsync(bucketName, objectKey, stream, file.Length, detectedContentType);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"خطا در آپلود '{fileName}': {ex.Message}");
            }
        }

        if (successCount > 0)
        {
            TempData["Success"] = $"{successCount} فایل با موفقیت آپلود شد.";
        }
        if (errors.Count > 0)
        {
            TempData["Error"] = string.Join("<br/>", errors);
        }

        return RedirectToAction(nameof(Index), new { bucketName, prefix });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFolder(CreateFolderRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.BucketName) || string.IsNullOrWhiteSpace(model.FolderName))
        {
            TempData["Error"] = "نام پوشه یا باکت نامعتبر است.";
            return RedirectToAction(nameof(Index), new { bucketName = model.BucketName, prefix = model.CurrentPrefix });
        }

        try
        {
            await _minioService.CreateFolderAsync(model.BucketName, model.CurrentPrefix, model.FolderName);
            TempData["Success"] = $"پوشه '{model.FolderName}' با موفقیت ایجاد شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در ایجاد پوشه: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { bucketName = model.BucketName, prefix = model.CurrentPrefix });
    }

    [HttpGet]
    public async Task<IActionResult> Download(string bucketName, string key)
    {
        if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
        {
            return BadRequest("پارامترهای درخواست ناقص است.");
        }

        try
        {
            var (stream, contentType, fileName) = await _minioService.DownloadObjectAsync(bucketName, key);
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در دانلود فایل: {ex.Message}";
            return RedirectToAction(nameof(Index), new { bucketName });
        }
    }

    [HttpGet]
    public async Task<IActionResult> StreamMedia(string bucketName, string key)
    {
        if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
        {
            return BadRequest();
        }

        try
        {
            var (stream, contentType, fileName) = await _minioService.DownloadObjectAsync(bucketName, key);
            var mimeType = GetExactMimeType(fileName, contentType);

            Response.Headers.Append("Accept-Ranges", "bytes");
            Response.Headers.Append("Content-Disposition", "inline; filename=\"" + Uri.EscapeDataString(fileName) + "\"");

            return File(stream, mimeType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    public static string GetExactMimeType(string fileName, string? defaultType = null)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            // Audio
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" or ".oga" => "audio/ogg",
            ".aac" => "audio/aac",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            ".weba" => "audio/webm",
            ".opus" => "audio/opus",
            ".wma" => "audio/x-ms-wma",

            // Video
            ".mp4" or ".m4v" => "video/mp4",
            ".webm" => "video/webm",
            ".ogv" => "video/ogg",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".ts" => "video/mp2t",

            // Images
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",

            // Documents
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".txt" or ".log" => "text/plain; charset=utf-8",
            ".csv" => "text/csv; charset=utf-8",
            ".xml" => "application/xml",

            _ => !string.IsNullOrWhiteSpace(defaultType) && defaultType != "application/octet-stream"
                ? defaultType
                : "application/octet-stream"
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetTextPreview(string bucketName, string key)
    {
        try
        {
            var text = await _minioService.GetObjectTextContentAsync(bucketName, key);
            return Ok(new { success = true, content = text });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GetPresignedUrl([FromBody] PresignedUrlRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.BucketName) || string.IsNullOrWhiteSpace(request.ObjectName))
        {
            return BadRequest(new { success = false, message = "پارامترهای درخواست نامعتبر است." });
        }

        try
        {
            int expirySeconds = Math.Clamp(request.ExpiryHours, 1, 168) * 3600; // max 7 days
            var url = await _minioService.GetPresignedUrlAsync(request.BucketName, request.ObjectName, expirySeconds);
            return Ok(new { success = true, url });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string bucketName, string key, string? prefix)
    {
        try
        {
            await _minioService.DeleteObjectAsync(bucketName, key);
            TempData["Success"] = "فایل با موفقیت حذف شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در حذف فایل: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { bucketName, prefix });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFolder(string bucketName, string folderPrefix, string? prefix)
    {
        try
        {
            await _minioService.DeleteFolderAsync(bucketName, folderPrefix);
            TempData["Success"] = "پوشه و تمام فایل‌های داخل آن با موفقیت حذف شدند.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در حذف پوشه: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { bucketName, prefix });
    }
}
