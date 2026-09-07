using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using minio_csharpClient.Models;
using minio_csharpClient.Services;

namespace minio_csharpClient.Controllers;

public class AccountController : Controller
{
    private readonly IUserService _userService;
    private readonly IActivityLogService _logService;

    public AccountController(IUserService userService, IActivityLogService logService)
    {
        _userService = userService;
        _logService = logService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userService.AuthenticateAsync(model.Username, model.Password);
        if (user != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role),
                new("CanViewAuditLogs", (user.Role == "Admin" || user.CanViewAuditLogs).ToString())
            };

            if (!string.IsNullOrWhiteSpace(user.AllowedBucket))
            {
                claims.Add(new Claim("AllowedBucket", user.AllowedBucket));
            }

            if (!string.IsNullOrWhiteSpace(user.AllowedPrefix))
            {
                claims.Add(new Claim("AllowedPrefix", user.AllowedPrefix));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            await _logService.LogAsync(
                username: user.Username,
                action: "ورود به سامانه",
                category: "Auth",
                details: $"ورود موفق با نقش {user.Role}",
                ipAddress: ip,
                isSuccess: true,
                logLevel: "Info"
            );

            return RedirectToLocal(returnUrl);
        }

        await _logService.LogAsync(
            username: model.Username,
            action: "تلاش ناموفق برای ورود",
            category: "Auth",
            details: "رمز عبور نادرست یا حساب کاربری غیرفعال است.",
            ipAddress: ip,
            isSuccess: false,
            logLevel: "Warning"
        );

        ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور اشتباه است یا حساب کاربری غیرفعال می‌باشد.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var username = User.Identity?.Name ?? "ناشناس";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _logService.LogAsync(
            username: username,
            action: "خروج از سامانه",
            category: "Auth",
            details: "خروج کاربر از حساب",
            ipAddress: ip,
            isSuccess: true,
            logLevel: "Info"
        );

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction(nameof(Login));
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _userService.ChangePasswordAsync(username, model.CurrentPassword, model.NewPassword);
        if (result.Success)
        {
            await _logService.LogAsync(
                username: username,
                action: "تغییر رمز عبور",
                category: "Auth",
                details: "کاربر رمز عبور خود را با موفقیت تغییر داد.",
                ipAddress: ip,
                isSuccess: true,
                logLevel: "Info"
            );

            TempData["SuccessMessage"] = "رمز عبور شما با موفقیت تغییر یافت.";
            return RedirectToAction("Index", "Home");
        }

        await _logService.LogAsync(
            username: username,
            action: "تلاش ناموفق برای تغییر رمز عبور",
            category: "Auth",
            details: result.ErrorMessage ?? "خطا در تغییر رمز عبور",
            ipAddress: ip,
            isSuccess: false,
            logLevel: "Warning"
        );

        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "خطایی رخ داد.");
        return View(model);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
