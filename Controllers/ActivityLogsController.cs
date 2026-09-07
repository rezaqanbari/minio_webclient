using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

namespace minio_csharpClient.Controllers;

[Authorize]
public class ActivityLogsController : Controller
{
    private readonly IActivityLogService _logService;

    public ActivityLogsController(IActivityLogService logService)
    {
        _logService = logService;
    }

    private bool HasAccess()
    {
        return User.IsInRole("Admin") || User.HasClaim(c => c.Type == "CanViewAuditLogs" && c.Value == "True");
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? username, string? category, string? logLevel, bool? isSuccess, string? search, int page = 1, int pageSize = 50)
    {
        if (!HasAccess())
        {
            return Forbid();
        }

        var model = await _logService.GetLogsAsync(username, category, logLevel, isSuccess, search, page, pageSize);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Clear(int daysToKeep = 30)
    {
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var deleted = await _logService.ClearOldLogsAsync(daysToKeep);
        TempData["Success"] = $"تعداد {deleted} رکورد لاگ قدیمی‌تر از {daysToKeep} روز پاک‌سازی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> LogClientError([FromBody] ClientErrorLogRequest request)
    {
        if (request == null)
        {
            return BadRequest();
        }

        var username = User.Identity?.Name ?? "ناشناس";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var details = $"{request.Details ?? "-"} {(request.StatusCode.HasValue ? $"| کد وضعیت: {request.StatusCode}" : "")}";

        await _logService.LogAsync(
            username: username,
            action: string.IsNullOrWhiteSpace(request.Action) ? "خطای سمت کلاینت" : request.Action,
            category: string.IsNullOrWhiteSpace(request.Category) ? "ClientError" : request.Category,
            details: details,
            ipAddress: ip,
            isSuccess: false,
            logLevel: "Error",
            errorMessage: request.ErrorMessage
        );

        return Ok(new { success = true });
    }
}
