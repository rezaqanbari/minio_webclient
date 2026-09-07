using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

var builder = WebApplication.CreateBuilder(args);

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

// Configure Authentication Credentials
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

// Configure large file uploads (up to 500 MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524288000;
});

// Cookie Authentication
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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
