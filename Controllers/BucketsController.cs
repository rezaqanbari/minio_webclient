using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

namespace minio_csharpClient.Controllers;

[Authorize]
public class BucketsController : Controller
{
    private readonly IMinioService _minioService;
    private readonly IActivityLogService _logService;

    public BucketsController(IMinioService minioService, IActivityLogService logService)
    {
        _minioService = minioService;
        _logService = logService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var buckets = await _minioService.GetBucketsAsync();

            if (!User.IsInRole("Admin"))
            {
                var allowedBucket = User.FindFirst("AllowedBucket")?.Value;
                if (string.IsNullOrWhiteSpace(allowedBucket))
                {
                    TempData["Error"] = "هیچ باکتی برای حساب کاربری شما تعریف نشده است. لطفاً با مدیر سیستم تماس بگیرید.";
                    return View(new List<BucketViewModel>());
                }

                buckets = buckets.Where(b => b.Name.Equals(allowedBucket, StringComparison.OrdinalIgnoreCase)).ToList();

                var allowedPrefix = User.FindFirst("AllowedPrefix")?.Value;
                ViewData["AllowedPrefix"] = allowedPrefix;
            }

            return View(buckets);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در دریافت لیست باکت‌ها: {ex.Message}";
            return View(new List<BucketViewModel>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateBucketRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.BucketName))
        {
            TempData["Error"] = "نام باکت نمی‌تواند خالی باشد.";
            return RedirectToAction(nameof(Index));
        }

        var name = model.BucketName.Trim().ToLowerInvariant();

        // Validate S3/MinIO bucket naming conventions
        if (!Regex.IsMatch(name, "^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$"))
        {
            TempData["Error"] = "نام باکت باید بین ۳ تا ۶۳ کاراکتر، شامل حروف کوچک انگلیسی، اعداد، نقطه یا خط تیره باشد و با حرف یا عدد شروع و تمام شود.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _minioService.CreateBucketAsync(name);

            await _logService.LogAsync(
                username: User.Identity?.Name ?? "Admin",
                action: "ایجاد باکت جدید",
                category: "Bucket",
                details: $"ایجاد باکت با نام '{name}'",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                isSuccess: true,
                logLevel: "Info"
            );

            TempData["Success"] = $"باکت '{name}' با موفقیت ایجاد شد.";
        }
        catch (Exception ex)
        {
            await _logService.LogAsync(
                username: User.Identity?.Name ?? "Admin",
                action: "خطا در ایجاد باکت",
                category: "Bucket",
                details: $"شکست در ایجاد باکت '{name}'",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                isSuccess: false,
                logLevel: "Error",
                errorMessage: ex.ToString()
            );

            TempData["Error"] = $"خطا در ایجاد باکت: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            TempData["Error"] = "نام باکت مشخص نشده است.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _minioService.DeleteBucketAsync(bucketName);

            await _logService.LogAsync(
                username: User.Identity?.Name ?? "Admin",
                action: "حذف باکت",
                category: "Bucket",
                details: $"حذف کامل باکت '{bucketName}'",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                isSuccess: true,
                logLevel: "Warning"
            );

            TempData["Success"] = $"باکت '{bucketName}' با موفقیت حذف شد.";
        }
        catch (Exception ex)
        {
            await _logService.LogAsync(
                username: User.Identity?.Name ?? "Admin",
                action: "خطا در حذف باکت",
                category: "Bucket",
                details: $"شکست در حذف باکت '{bucketName}'",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                isSuccess: false,
                logLevel: "Error",
                errorMessage: ex.ToString()
            );

            TempData["Error"] = $"خطا در حذف باکت '{bucketName}': توجه داشته باشید باکت باید خالی از فایل باشد تا حذف شود. جزئیات: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
