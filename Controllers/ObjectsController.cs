using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

namespace minio_csharpClient.Controllers;

[Authorize]
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

        if (!IsAuthorizedForBucket(bucketName))
        {
            return Forbid();
        }

        prefix = GetEffectivePrefix(prefix);

        try
        {
            var model = await _minioService.GetBucketContentAsync(bucketName, prefix);
            ApplyPrefixScopingToModel(model);
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

        if (!IsAuthorizedForBucket(bucketName))
        {
            return Forbid();
        }

        prefix = GetEffectivePrefix(prefix) ?? string.Empty;
        if (!IsAuthorizedForPrefix(prefix))
        {
            return Forbid();
        }

        if (files == null || files.Count == 0)
        {
            TempData["Error"] = "هیچ فایلی برای آپلود انتخاب نشده است.";
            return RedirectToAction(nameof(Index), new { bucketName, prefix });
        }

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
    [RequestSizeLimit(524288000)] // 500 MB limit
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
    public async Task<IActionResult> UploadSingle(string bucketName, string? prefix, IFormFile file)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return BadRequest(new { success = false, message = "نام باکت مشخص نشده است." });
        }

        if (!IsAuthorizedForBucket(bucketName))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "عدم دسترسی به باکت" });
        }

        prefix = GetEffectivePrefix(prefix) ?? string.Empty;
        if (!IsAuthorizedForPrefix(prefix))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "عدم دسترسی به این پوشه" });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "هیچ فایلی برای آپلود انتخاب نشده است." });
        }

        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith('/'))
        {
            prefix += "/";
        }

        string fileName = Path.GetFileName(file.FileName);
        string objectKey = $"{prefix}{fileName}";

        try
        {
            using var stream = file.OpenReadStream();
            var detectedContentType = GetExactMimeType(fileName, file.ContentType);
            await _minioService.UploadObjectAsync(bucketName, objectKey, stream, file.Length, detectedContentType);
            return Ok(new { success = true, fileName, size = file.Length });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFolder(CreateFolderRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.BucketName))
        {
            TempData["Error"] = "نام باکت مشخص نشده است.";
            return RedirectToAction("Index", "Buckets");
        }

        if (!IsAuthorizedForBucket(model.BucketName))
        {
            return Forbid();
        }

        var effectivePrefix = GetEffectivePrefix(model.CurrentPrefix) ?? string.Empty;
        if (!IsAuthorizedForPrefix(effectivePrefix))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.FolderName))
        {
            TempData["Error"] = "نام پوشه نمی‌تواند خالی باشد.";
            return RedirectToAction(nameof(Index), new { bucketName = model.BucketName, prefix = effectivePrefix });
        }

        try
        {
            await _minioService.CreateFolderAsync(model.BucketName, effectivePrefix, model.FolderName);
            TempData["Success"] = $"پوشه '{model.FolderName}' با موفقیت ساخته شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در ایجاد پوشه: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { bucketName = model.BucketName, prefix = effectivePrefix });
    }

    [HttpGet]
    public async Task<IActionResult> Download(string bucketName, string key)
    {
        if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(key))
        {
            return BadRequest("پارامترهای درخواست ناقص است.");
        }

        if (!IsAuthorizedForBucket(bucketName) || !IsAuthorizedForPrefix(key))
        {
            return Forbid();
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

        if (!IsAuthorizedForBucket(bucketName) || !IsAuthorizedForPrefix(key))
        {
            return Forbid();
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
        if (!IsAuthorizedForBucket(bucketName) || !IsAuthorizedForPrefix(key))
        {
            return Forbid();
        }

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

        if (!IsAuthorizedForBucket(request.BucketName) || !IsAuthorizedForPrefix(request.ObjectName))
        {
            return Forbid();
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
        if (!IsAuthorizedForBucket(bucketName) || !IsAuthorizedForPrefix(key))
        {
            return Forbid();
        }

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
        if (!IsAuthorizedForBucket(bucketName) || !IsAuthorizedForPrefix(folderPrefix))
        {
            return Forbid();
        }

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

    #region Scope Authorization Helpers

    private bool IsAuthorizedForBucket(string bucketName)
    {
        if (User.IsInRole("Admin")) return true;
        var allowedBucket = User.FindFirst("AllowedBucket")?.Value;
        return !string.IsNullOrWhiteSpace(allowedBucket) &&
               string.Equals(allowedBucket, bucketName, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAuthorizedForPrefix(string? prefixOrKey)
    {
        if (User.IsInRole("Admin")) return true;
        var allowedPrefix = User.FindFirst("AllowedPrefix")?.Value;
        if (string.IsNullOrWhiteSpace(allowedPrefix)) return true; // Scoped to entire bucket

        if (string.IsNullOrWhiteSpace(prefixOrKey)) return false; // Cannot access bucket root if scoped to a subfolder

        var normAllowed = allowedPrefix.Trim().TrimStart('/');
        if (!normAllowed.EndsWith('/')) normAllowed += "/";

        var normTarget = prefixOrKey.Trim().TrimStart('/');
        return normTarget.StartsWith(normAllowed, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetEffectivePrefix(string? requestedPrefix)
    {
        if (User.IsInRole("Admin")) return requestedPrefix;
        var allowedPrefix = User.FindFirst("AllowedPrefix")?.Value;
        if (string.IsNullOrWhiteSpace(allowedPrefix)) return requestedPrefix;

        if (string.IsNullOrWhiteSpace(requestedPrefix) || !IsAuthorizedForPrefix(requestedPrefix))
        {
            return allowedPrefix;
        }

        return requestedPrefix;
    }

    private void ApplyPrefixScopingToModel(BucketContentViewModel model)
    {
        if (User.IsInRole("Admin")) return;
        var allowedPrefix = User.FindFirst("AllowedPrefix")?.Value;
        if (string.IsNullOrWhiteSpace(allowedPrefix)) return;

        var normAllowed = allowedPrefix.Trim().TrimStart('/');
        if (!normAllowed.EndsWith('/')) normAllowed += "/";

        // Disable parent navigation if already at allowed root prefix
        var normCurrent = model.CurrentPrefix.TrimStart('/');
        if (string.Equals(normCurrent, normAllowed, StringComparison.OrdinalIgnoreCase))
        {
            model.ParentPrefix = null;
        }
        else if (model.ParentPrefix != null)
        {
            var normParent = model.ParentPrefix.TrimStart('/');
            if (!normParent.StartsWith(normAllowed, StringComparison.OrdinalIgnoreCase) &&
                !normParent.Equals(normAllowed.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                model.ParentPrefix = normAllowed;
            }
        }

        // Adjust breadcrumbs to not expose parents above allowedPrefix
        var scopedBreadcrumbs = new List<BreadcrumbItem>();
        foreach (var b in model.Breadcrumbs)
        {
            if (string.IsNullOrEmpty(b.Prefix))
            {
                // Root bucket breadcrumb: if user is restricted to a prefix, link root breadcrumb to that prefix
                scopedBreadcrumbs.Add(new BreadcrumbItem
                {
                    Title = normAllowed.TrimEnd('/'),
                    Prefix = normAllowed,
                    IsActive = string.Equals(normCurrent, normAllowed, StringComparison.OrdinalIgnoreCase)
                });
            }
            else if (b.Prefix.StartsWith(normAllowed, StringComparison.OrdinalIgnoreCase))
            {
                scopedBreadcrumbs.Add(b);
            }
        }

        if (scopedBreadcrumbs.Count > 0)
        {
            model.Breadcrumbs = scopedBreadcrumbs;
        }
    }

    #endregion
}
