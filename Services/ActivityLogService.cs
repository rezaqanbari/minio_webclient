using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using minio_csharpClient.Data;
using minio_csharpClient.Entities;
using minio_csharpClient.Models;

namespace minio_csharpClient.Services;

public class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(AppDbContext dbContext, ILogger<ActivityLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(string username, string action, string category, string details, string? ipAddress = null, bool isSuccess = true, string logLevel = "Info", string? errorMessage = null)
    {
        try
        {
            var log = new AppActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Username = string.IsNullOrWhiteSpace(username) ? "ناشناس" : username.Trim(),
                Action = action,
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                Details = details ?? string.Empty,
                IpAddress = ipAddress,
                IsSuccess = isSuccess,
                LogLevel = logLevel,
                ErrorMessage = errorMessage
            };

            _dbContext.ActivityLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist activity log to database.");
        }
    }

    public async Task LogExceptionAsync(Exception ex, string? username = null, string? path = null, string? ipAddress = null)
    {
        try
        {
            var details = $"مسیر درخواست: {path ?? "-"} | نوع خطا: {ex.GetType().Name}";
            var log = new AppActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Username = string.IsNullOrWhiteSpace(username) ? "سیستم" : username.Trim(),
                Action = "خطای سرور",
                Category = "SystemError",
                Details = details,
                IpAddress = ipAddress,
                IsSuccess = false,
                LogLevel = "Error",
                ErrorMessage = $"{ex.Message}\n\nStackTrace:\n{ex.StackTrace}"
            };

            _dbContext.ActivityLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to persist exception log to database.");
        }
    }

    public async Task<ActivityLogIndexViewModel> GetLogsAsync(string? username, string? category, string? logLevel, bool? isSuccess, string? search, int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 10) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<AppActivityLog> query = _dbContext.ActivityLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(username))
        {
            query = query.Where(l => l.Username == username);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(l => l.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(logLevel))
        {
            query = query.Where(l => l.LogLevel == logLevel);
        }

        if (isSuccess.HasValue)
        {
            query = query.Where(l => l.IsSuccess == isSuccess.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(l => l.Action.ToLower().Contains(s) ||
                                     l.Details.ToLower().Contains(s) ||
                                     (l.ErrorMessage != null && l.ErrorMessage.ToLower().Contains(s)) ||
                                     (l.IpAddress != null && l.IpAddress.ToLower().Contains(s)));
        }

        int totalCount = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages < 1) totalPages = 1;

        var logs = await query.OrderByDescending(l => l.Timestamp)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync();

        var users = await _dbContext.ActivityLogs
                                    .Select(l => l.Username)
                                    .Distinct()
                                    .OrderBy(u => u)
                                    .Take(100)
                                    .ToListAsync();

        var categories = await _dbContext.ActivityLogs
                                         .Select(l => l.Category)
                                         .Distinct()
                                         .OrderBy(c => c)
                                         .Take(50)
                                         .ToListAsync();

        var stats = await GetStatsAsync();

        return new ActivityLogIndexViewModel
        {
            Logs = logs,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PageSize = pageSize,
            SelectedUser = username,
            SelectedCategory = category,
            SelectedLogLevel = logLevel,
            SelectedIsSuccess = isSuccess,
            SearchQuery = search,
            Users = users,
            Categories = categories,
            Stats = stats
        };
    }

    public async Task<ActivityLogStatsViewModel> GetStatsAsync()
    {
        var startOfToday = DateTime.UtcNow.Date;
        var endOfToday = startOfToday.AddDays(1);

        var todayQuery = _dbContext.ActivityLogs.Where(l => l.Timestamp >= startOfToday && l.Timestamp < endOfToday);

        int todayTotal = await todayQuery.CountAsync();
        int todayErrors = await todayQuery.CountAsync(l => l.LogLevel == "Error" || !l.IsSuccess);
        int todayUploads = await todayQuery.CountAsync(l => l.Category == "File" && l.Action.Contains("آپلود"));
        int todayLogins = await todayQuery.CountAsync(l => l.Category == "Auth" && l.Action.Contains("ورود"));

        return new ActivityLogStatsViewModel
        {
            TodayTotal = todayTotal,
            TodayErrors = todayErrors,
            TodayUploads = todayUploads,
            TodayLogins = todayLogins
        };
    }

    public async Task<int> ClearOldLogsAsync(int daysToKeep = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        var oldLogs = await _dbContext.ActivityLogs.Where(l => l.Timestamp < cutoff).ToListAsync();
        if (oldLogs.Count > 0)
        {
            _dbContext.ActivityLogs.RemoveRange(oldLogs);
            return await _dbContext.SaveChangesAsync();
        }
        return 0;
    }
}
