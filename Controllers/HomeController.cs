using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

namespace minio_csharpClient.Controllers;

public class HomeController : Controller
{
    private readonly IMinioService _minioService;
    private readonly MinioOptions _minioOptions;

    public HomeController(IMinioService minioService, IOptions<MinioOptions> minioOptions)
    {
        _minioService = minioService;
        _minioOptions = minioOptions.Value;
    }

    public async Task<IActionResult> Index()
    {
        bool isOnline = false;
        List<BucketViewModel> buckets = new();
        string? errorMessage = null;

        try
        {
            isOnline = await _minioService.PingAsync();
            if (isOnline)
            {
                buckets = await _minioService.GetBucketsAsync();

                if (!User.IsInRole("Admin"))
                {
                    var allowedBucket = User.FindFirst("AllowedBucket")?.Value;
                    if (string.IsNullOrWhiteSpace(allowedBucket))
                    {
                        buckets.Clear();
                    }
                    else
                    {
                        buckets = buckets.Where(b => b.Name.Equals(allowedBucket, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        ViewBag.IsOnline = isOnline;
        ViewBag.Endpoint = _minioOptions.Endpoint;
        ViewBag.WithSSL = _minioOptions.WithSSL;
        ViewBag.ErrorMessage = errorMessage;

        return View(buckets);
    }

    [HttpGet]
    public async Task<IActionResult> GlobalSearch(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(new { success = true, count = 0, results = new List<object>() });
        }

        try
        {
            var buckets = await _minioService.GetBucketsAsync();
            var bucketNames = buckets.Select(b => b.Name).ToList();
            string? scopedPrefix = null;

            if (!User.IsInRole("Admin"))
            {
                var allowedBucket = User.FindFirst("AllowedBucket")?.Value;
                if (string.IsNullOrWhiteSpace(allowedBucket))
                {
                    return Json(new { success = true, count = 0, results = new List<object>() });
                }

                bucketNames = bucketNames.Where(b => b.Equals(allowedBucket, StringComparison.OrdinalIgnoreCase)).ToList();
                scopedPrefix = User.FindFirst("AllowedPrefix")?.Value;
            }

            var results = await _minioService.SearchObjectsAcrossBucketsAsync(bucketNames, q, scopedPrefix);

            var data = results.Select(r => new
            {
                bucketName = r.BucketName,
                key = r.Key,
                fileName = Path.GetFileName(r.Key),
                size = r.Size,
                formattedSize = r.FormattedSize,
                lastModified = r.FormattedLastModified,
                iconClass = r.IconClass,
                previewType = r.PreviewType,
                isPreviewable = r.IsPreviewable,
                folderPath = Path.GetDirectoryName(r.Key)?.Replace("\\", "/") ?? ""
            });

            return Json(new { success = true, count = results.Count, results = data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
