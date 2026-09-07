using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using minio_csharpClient.Data;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite Database
var dbDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
if (!Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}
var dbPath = Path.Combine(dbDirectory, "app.db");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Register Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

// Configure MinIO Options (from appsettings and environment variables)
builder.Services.Configure<MinioOptions>(options =>
{
    builder.Configuration.GetSection(MinioOptions.SectionName).Bind(options);

    var envEndpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT");
    if (!string.IsNullOrWhiteSpace(envEndpoint)) options.Endpoint = envEndpoint;

    var envAccessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY");
    if (!string.IsNullOrWhiteSpace(envAccessKey)) options.AccessKey = envAccessKey;

    var envSecretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY");
    if (!string.IsNullOrWhiteSpace(envSecretKey)) options.SecretKey = envSecretKey;

    var envWithSSL = Environment.GetEnvironmentVariable("MINIO_WITH_SSL");
    if (!string.IsNullOrWhiteSpace(envWithSSL) && bool.TryParse(envWithSSL, out var withSsl)) options.WithSSL = withSsl;

    var envRegion = Environment.GetEnvironmentVariable("MINIO_REGION");
    if (!string.IsNullOrWhiteSpace(envRegion)) options.Region = envRegion;
});

// Configure Authentication Credentials (used for initial admin seeding fallback)
builder.Services.Configure<AuthOptions>(options =>
{
    builder.Configuration.GetSection(AuthOptions.SectionName).Bind(options);

    var envUser = Environment.GetEnvironmentVariable("AUTH_USERNAME");
    if (!string.IsNullOrWhiteSpace(envUser)) options.Username = envUser;

    var envPass = Environment.GetEnvironmentVariable("AUTH_PASSWORD");
    if (!string.IsNullOrWhiteSpace(envPass)) options.Password = envPass;
});

// Register MinIO Service
builder.Services.AddSingleton<IMinioService, MinioService>();

// Configure Kestrel limits (up to 500 MB)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524288000; // 500 MB
});

// Configure large file uploads (up to 500 MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524288000;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 524288000;
});

// Cookie Authentication with RELATIVE redirects (prevents redirecting to localhost behind reverse proxies)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                var returnUrl = context.Request.Path + context.Request.QueryString;
                var loginUrl = QueryHelpers.AddQueryString(options.LoginPath.ToString(), options.ReturnUrlParameter, returnUrl);
                context.Response.Headers.Location = loginUrl;
                context.Response.StatusCode = StatusCodes.Status302Found;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.Headers.Location = options.AccessDeniedPath.ToString();
                context.Response.StatusCode = StatusCodes.Status302Found;
                return Task.CompletedTask;
            }
        };
    });

// Fallback Authorization Policy (protect all pages by default unless marked [AllowAnonymous])
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Add MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Ensure Database is initialized and Seed Default Admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
    var authOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;
    userService.SeedDefaultAdminAsync(authOptions.Username, authOptions.Password).GetAwaiter().GetResult();
}

// Configure Forwarded Headers for Nginx / Reverse Proxy / Dynamic Domains
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.All
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Global Exception Logging Middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        try
        {
            var logService = context.RequestServices.GetService<IActivityLogService>();
            if (logService != null)
            {
                var username = context.User?.Identity?.Name ?? "ناشناس";
                var ip = context.Connection.RemoteIpAddress?.ToString();
                await logService.LogExceptionAsync(ex, username, context.Request.Path, ip);
            }
        }
        catch { }
        throw;
    }
});

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
