using System;
using System.Threading.Tasks;
using minio_csharpClient.Models;

namespace minio_csharpClient.Services;

public interface IActivityLogService
{
    Task LogAsync(string username, string action, string category, string details, string? ipAddress = null, bool isSuccess = true, string logLevel = "Info", string? errorMessage = null);
    Task LogExceptionAsync(Exception ex, string? username = null, string? path = null, string? ipAddress = null);
    Task<ActivityLogIndexViewModel> GetLogsAsync(string? username, string? category, string? logLevel, bool? isSuccess, string? search, int page = 1, int pageSize = 50);
    Task<ActivityLogStatsViewModel> GetStatsAsync();
    Task<int> ClearOldLogsAsync(int daysToKeep = 30);
}
