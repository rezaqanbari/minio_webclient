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

// Register MinIO Service
builder.Services.AddSingleton<IMinioService, MinioService>();

// Configure large file uploads (up to 500 MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524288000;
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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
