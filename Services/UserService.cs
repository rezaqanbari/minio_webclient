using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using minio_csharpClient.Data;
using minio_csharpClient.Entities;
using minio_csharpClient.Models;

namespace minio_csharpClient.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<AppUser> _passwordHasher;

    public UserService(AppDbContext db)
    {
        _db = db;
        _passwordHasher = new PasswordHasher<AppUser>();
    }

    public async Task<AppUser?> AuthenticateAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower());
        if (user == null || !user.IsActive)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            return user;
        }

        return null;
    }

    public async Task<List<AppUser>> GetAllUsersAsync()
    {
        return await _db.Users.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<AppUser?> GetByIdAsync(Guid id)
    {
        return await _db.Users.FindAsync(id);
    }

    public async Task<AppUser?> GetByUsernameAsync(string username)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower());
    }

    public async Task<(bool Success, string? ErrorMessage)> CreateUserAsync(CreateUserViewModel model)
    {
        var normalizedUsername = model.Username.Trim();
        var exists = await _db.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername.ToLower());
        if (exists)
        {
            return (false, "کاربری با این نام کاربری قبلاً ثبت شده است.");
        }

        var user = new AppUser
        {
            Username = normalizedUsername,
            Role = model.Role == "Admin" ? "Admin" : "User",
            AllowedBucket = model.Role == "Admin" ? null : CleanBucket(model.AllowedBucket),
            AllowedPrefix = model.Role == "Admin" ? null : CleanPrefix(model.AllowedPrefix),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(EditUserViewModel model)
    {
        var user = await _db.Users.FindAsync(model.Id);
        if (user == null)
        {
            return (false, "کاربر مورد نظر یافت نشد.");
        }

        // If trying to demote or deactivate the last admin
        if (user.Role == "Admin" && (model.Role != "Admin" || !model.IsActive))
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == "Admin" && u.IsActive && u.Id != user.Id);
            if (adminCount == 0)
            {
                return (false, "امکان غیرفعال‌سازی یا تغییر نقش آخرین مدیر فعال سامانه وجود ندارد.");
            }
        }

        user.Role = model.Role == "Admin" ? "Admin" : "User";
        user.AllowedBucket = model.Role == "Admin" ? null : CleanBucket(model.AllowedBucket);
        user.AllowedPrefix = model.Role == "Admin" ? null : CleanPrefix(model.AllowedPrefix);
        user.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return (false, "کاربر مورد نظر یافت نشد.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(string username, string currentPassword, string newPassword)
    {
        var user = await GetByUsernameAsync(username);
        if (user == null)
        {
            return (false, "کاربر یافت نشد.");
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verify == PasswordVerificationResult.Failed)
        {
            return (false, "رمز عبور فعلی اشتباه است.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(Guid id, string currentUsername)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return (false, "کاربر مورد نظر یافت نشد.");
        }

        if (user.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "شما نمی‌توانید حساب کاربری جاری خود را حذف کنید.");
        }

        if (user.Role == "Admin")
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == "Admin" && u.Id != user.Id);
            if (adminCount == 0)
            {
                return (false, "امکان حذف آخرین مدیر سیستم وجود ندارد.");
            }
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task SeedDefaultAdminAsync(string? defaultUsername = null, string? defaultPassword = null)
    {
        if (!await _db.Users.AnyAsync())
        {
            var uname = string.IsNullOrWhiteSpace(defaultUsername) ? "admin" : defaultUsername.Trim();
            var pwd = string.IsNullOrWhiteSpace(defaultPassword) ? "Admin@123456" : defaultPassword.Trim();

            var admin = new AppUser
            {
                Username = uname,
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            admin.PasswordHash = _passwordHasher.HashPassword(admin, pwd);

            _db.Users.Add(admin);
            await _db.SaveChangesAsync();
        }
    }

    private static string? CleanBucket(string? bucket)
    {
        if (string.IsNullOrWhiteSpace(bucket)) return null;
        return bucket.Trim();
    }

    private static string? CleanPrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return null;
        var p = prefix.Trim().TrimStart('/');
        if (!p.EndsWith('/'))
        {
            p += "/";
        }
        return p;
    }
}
