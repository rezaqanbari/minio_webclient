using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace minio_csharpClient.Models;

public class UserItemViewModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? AllowedBucket { get; set; }
    public string? AllowedPrefix { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserViewModel
{
    [Required(ErrorMessage = "نام کاربری الزامی است.")]
    [Display(Name = "نام کاربری")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "نام کاربری باید بین ۳ تا ۵۰ کاراکتر باشد.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "رمز عبور باید حداقل ۶ کاراکتر باشد.")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "تکرار رمز عبور")]
    [Compare("Password", ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "نقش کاربر الزامی است.")]
    [Display(Name = "نقش")]
    public string Role { get; set; } = "User"; // "Admin" or "User"

    [Display(Name = "باکت مجاز")]
    public string? AllowedBucket { get; set; }

    [Display(Name = "پوشه یا پیشوند مجاز (Prefix)")]
    public string? AllowedPrefix { get; set; }

    public List<string> AvailableBuckets { get; set; } = new();
}

public class EditUserViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "نام کاربری")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "نقش کاربر الزامی است.")]
    [Display(Name = "نقش")]
    public string Role { get; set; } = "User";

    [Display(Name = "باکت مجاز")]
    public string? AllowedBucket { get; set; }

    [Display(Name = "پوشه یا پیشوند مجاز (Prefix)")]
    public string? AllowedPrefix { get; set; }

    [Display(Name = "وضعیت حساب کاربری")]
    public bool IsActive { get; set; } = true;

    public List<string> AvailableBuckets { get; set; } = new();
}

public class ResetPasswordViewModel
{
    public Guid UserId { get; set; }

    [Display(Name = "نام کاربری")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور جدید الزامی است.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور جدید")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "رمز عبور باید حداقل ۶ کاراکتر باشد.")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "تکرار رمز عبور جدید")]
    [Compare("NewPassword", ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "رمز عبور فعلی الزامی است.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور فعلی")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور جدید الزامی است.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور جدید")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "رمز عبور باید حداقل ۶ کاراکتر باشد.")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "تکرار رمز عبور جدید")]
    [Compare("NewPassword", ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
