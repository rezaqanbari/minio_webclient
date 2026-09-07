using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

namespace minio_csharpClient.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly IMinioService _minioService;

    public UsersController(IUserService userService, IMinioService minioService)
    {
        _userService = userService;
        _minioService = minioService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _userService.GetAllUsersAsync();
        var model = users.Select(u => new UserItemViewModel
        {
            Id = u.Id,
            Username = u.Username,
            Role = u.Role,
            AllowedBucket = u.AllowedBucket,
            AllowedPrefix = u.AllowedPrefix,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        }).ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateUserViewModel();
        await PopulateBuckets(model.AvailableBuckets);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBuckets(model.AvailableBuckets);
            return View(model);
        }

        var result = await _userService.CreateUserAsync(model);
        if (result.Success)
        {
            TempData["SuccessMessage"] = $"کاربر «{model.Username}» با موفقیت ایجاد شد.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "خطایی در ثبت کاربر رخ داد.");
        await PopulateBuckets(model.AvailableBuckets);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new EditUserViewModel
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            AllowedBucket = user.AllowedBucket,
            AllowedPrefix = user.AllowedPrefix,
            IsActive = user.IsActive
        };

        await PopulateBuckets(model.AvailableBuckets);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBuckets(model.AvailableBuckets);
            return View(model);
        }

        var result = await _userService.UpdateUserAsync(model);
        if (result.Success)
        {
            TempData["SuccessMessage"] = $"اطلاعات کاربر «{model.Username}» با موفقیت به‌روزرسانی شد.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "خطایی در به‌روزرسانی کاربر رخ داد.");
        await PopulateBuckets(model.AvailableBuckets);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new ResetPasswordViewModel
        {
            UserId = user.Id,
            Username = user.Username
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userService.ResetPasswordAsync(model.UserId, model.NewPassword);
        if (result.Success)
        {
            TempData["SuccessMessage"] = $"رمز عبور کاربر «{model.Username}» با موفقیت تغییر یافت.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "خطایی در بازنشانی رمز عبور رخ داد.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUsername = User.Identity?.Name ?? string.Empty;
        var result = await _userService.DeleteUserAsync(id, currentUsername);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "کاربر مورد نظر با موفقیت حذف گردید.";
        }
        else
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "خطایی در حذف کاربر رخ داد.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateBuckets(System.Collections.Generic.List<string> list)
    {
        try
        {
            var buckets = await _minioService.GetBucketsAsync();
            list.Clear();
            list.AddRange(buckets.Select(b => b.Name).OrderBy(n => n));
        }
        catch
        {
            // If listing buckets fails, leave empty
        }
    }
}
