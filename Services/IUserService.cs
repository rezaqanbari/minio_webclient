using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using minio_csharpClient.Entities;
using minio_csharpClient.Models;

namespace minio_csharpClient.Services;

public interface IUserService
{
    Task<AppUser?> AuthenticateAsync(string username, string password);
    Task<List<AppUser>> GetAllUsersAsync();
    Task<AppUser?> GetByIdAsync(Guid id);
    Task<AppUser?> GetByUsernameAsync(string username);
    Task<(bool Success, string? ErrorMessage)> CreateUserAsync(CreateUserViewModel model);
    Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(EditUserViewModel model);
    Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(Guid userId, string newPassword);
    Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(string username, string currentPassword, string newPassword);
    Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(Guid id, string currentUsername);
    Task SeedDefaultAdminAsync(string? defaultUsername = null, string? defaultPassword = null);
}
