using System;
using System.Collections.Generic;
using minio_csharpClient.Entities;

namespace minio_csharpClient.Models;

public class ActivityLogIndexViewModel
{
    public List<AppActivityLog> Logs { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; } = 0;
    public int PageSize { get; set; } = 50;

    // Filters
    public string? SelectedUser { get; set; }
    public string? SelectedCategory { get; set; }
    public string? SelectedLogLevel { get; set; }
    public bool? SelectedIsSuccess { get; set; }
    public string? SearchQuery { get; set; }

    // Dropdown lists
    public List<string> Users { get; set; } = new();
    public List<string> Categories { get; set; } = new();

    // Summary stats
    public ActivityLogStatsViewModel Stats { get; set; } = new();
}

public class ActivityLogStatsViewModel
{
    public int TodayTotal { get; set; }
    public int TodayErrors { get; set; }
    public int TodayUploads { get; set; }
    public int TodayLogins { get; set; }
}

public class ClientErrorLogRequest
{
    public string? Action { get; set; }
    public string? Category { get; set; }
    public string? Details { get; set; }
    public string? ErrorMessage { get; set; }
    public int? StatusCode { get; set; }
}
