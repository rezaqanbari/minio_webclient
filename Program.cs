using Microsoft.AspNetCore.Http.Features;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure MinIO Options
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection(MinioOptions.SectionName));

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
