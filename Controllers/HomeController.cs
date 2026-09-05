using System;
using System.Diagnostics;
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
