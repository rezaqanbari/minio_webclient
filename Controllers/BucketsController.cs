using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

namespace minio_csharpClient.Controllers;

public class BucketsController : Controller
{
    private readonly IMinioService _minioService;

    public BucketsController(IMinioService minioService)
    {
        _minioService = minioService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var buckets = await _minioService.GetBucketsAsync();
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
            TempData["Success"] = $"باکت '{name}' با موفقیت ایجاد شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در ایجاد باکت: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
            TempData["Success"] = $"باکت '{bucketName}' با موفقیت حذف شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در حذف باکت '{bucketName}': توجه داشته باشید باکت باید خالی از فایل باشد تا حذف شود. جزئیات: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
